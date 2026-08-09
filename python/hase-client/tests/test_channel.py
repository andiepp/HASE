import asyncio
from functools import wraps
from pathlib import Path
from typing import Any, Callable, Coroutine, TypeVar

import pytest

import hase.channel as channel_module
from hase import RuntimeHostChannelError
from hase import RuntimeHostProfile
from hase import open_runtime_host_channel


_T = TypeVar("_T")


def _async_test(
    function: Callable[..., Coroutine[Any, Any, _T]],
) -> Callable[..., _T]:
    @wraps(function)
    def run(*args: object, **kwargs: object) -> _T:
        return asyncio.run(function(*args, **kwargs))

    return run


class _FakeChannel:
    def __init__(self) -> None:
        self.ready = asyncio.Event()
        self.ready.set()
        self.ready_failure: Exception | None = None
        self.close_calls = 0
        self.close_started = asyncio.Event()
        self.close_gate: asyncio.Event | None = None
        self.close_failure: Exception | None = None

    async def channel_ready(self) -> None:
        if self.ready_failure is not None:
            raise self.ready_failure
        await self.ready.wait()

    async def close(self) -> None:
        self.close_calls += 1
        self.close_started.set()
        if self.close_failure is not None:
            raise self.close_failure
        if self.close_gate is not None:
            await self.close_gate.wait()


def _profile(tmp_path: Path) -> RuntimeHostProfile:
    certificate = tmp_path / "client-chain.pem"
    private_key = tmp_path / "client-key.pem"
    trusted_server = tmp_path / "trusted-server.cer"
    certificate.write_bytes(b"exact certificate")
    private_key.write_bytes(b"exact private key")
    trusted_server.write_bytes(
        b"-----BEGIN CERTIFICATE-----\n"
        b"exact trusted server\n"
        b"-----END CERTIFICATE-----\n"
    )
    return RuntimeHostProfile(
        1,
        "https://192.0.2.10:50443",
        certificate,
        private_key,
        trusted_server,
    )


def _install_fakes(
    monkeypatch: pytest.MonkeyPatch,
    fake_channel: _FakeChannel,
) -> dict[str, Any]:
    captured: dict[str, Any] = {}

    def credentials(**values: object) -> object:
        captured["credentials"] = values
        return "credentials"

    def secure_channel(
        target: str,
        channel_credentials: object,
        *,
        options: object,
    ) -> _FakeChannel:
        captured["target"] = target
        captured["channel_credentials"] = channel_credentials
        captured["options"] = options
        return fake_channel

    monkeypatch.setattr(channel_module.grpc, "ssl_channel_credentials", credentials)
    monkeypatch.setattr(channel_module.grpc.aio, "secure_channel", secure_channel)
    return captured


@_async_test
async def test_open_forwards_exact_credentials_and_target(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    profile = _profile(tmp_path)
    fake = _FakeChannel()
    captured = _install_fakes(monkeypatch, fake)

    opened = await open_runtime_host_channel(profile)

    assert captured["target"] == "192.0.2.10:50443"
    assert captured["credentials"] == {
        "root_certificates": (
            b"-----BEGIN CERTIFICATE-----\n"
            b"exact trusted server\n"
            b"-----END CERTIFICATE-----\n"
        ),
        "private_key": b"exact private key",
        "certificate_chain": b"exact certificate",
    }
    assert captured["channel_credentials"] == "credentials"
    assert captured["options"] == (("grpc.enable_retries", 0),)
    assert opened.grpc_channel is fake
    await opened.close()


@_async_test
async def test_open_preserves_bracketed_ipv6_target(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    profile = _profile(tmp_path)
    profile = RuntimeHostProfile(
        profile.format_version,
        "https://[2001:db8::10]:50443",
        profile.client_certificate_chain_path,
        profile.client_private_key_path,
        profile.trusted_server_certificate_path,
    )
    captured = _install_fakes(monkeypatch, _FakeChannel())

    opened = await open_runtime_host_channel(profile)

    assert captured["target"] == "[2001:db8::10]:50443"
    await opened.close()


@_async_test
async def test_open_rejects_missing_or_oversized_credentials(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    profile = _profile(tmp_path)
    profile.client_private_key_path.unlink()

    with pytest.raises(RuntimeHostChannelError) as missing:
        await open_runtime_host_channel(profile)
    assert missing.value.code == "credential-file-unavailable"

    profile.client_private_key_path.write_bytes(b"x" * ((128 * 1024) + 1))
    with pytest.raises(RuntimeHostChannelError) as oversized:
        await open_runtime_host_channel(profile)
    assert oversized.value.code == "credential-file-size-invalid"
    assert str(profile.client_private_key_path) not in str(oversized.value)


@_async_test
async def test_open_converts_der_trusted_certificate_to_pem(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    profile = _profile(tmp_path)
    profile.trusted_server_certificate_path.write_bytes(b"exact der")
    monkeypatch.setattr(
        channel_module.ssl,
        "DER_cert_to_PEM_cert",
        lambda value: "-----BEGIN CERTIFICATE-----\nconverted\n"
        "-----END CERTIFICATE-----\n"
        if value == b"exact der"
        else "unexpected",
    )
    captured = _install_fakes(monkeypatch, _FakeChannel())

    opened = await open_runtime_host_channel(profile)

    assert captured["credentials"]["root_certificates"] == (
        b"-----BEGIN CERTIFICATE-----\nconverted\n"
        b"-----END CERTIFICATE-----\n"
    )
    await opened.close()


@_async_test
async def test_open_timeout_closes_channel(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    fake = _FakeChannel()
    fake.ready.clear()
    _install_fakes(monkeypatch, fake)

    with pytest.raises(RuntimeHostChannelError) as captured:
        await open_runtime_host_channel(_profile(tmp_path), readiness_timeout=0.01)

    assert captured.value.code == "channel-readiness-timeout"
    assert fake.close_calls == 1


@_async_test
async def test_open_cancellation_closes_channel_and_propagates(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    fake = _FakeChannel()
    fake.ready.clear()
    _install_fakes(monkeypatch, fake)
    task = asyncio.create_task(
        open_runtime_host_channel(_profile(tmp_path), readiness_timeout=30)
    )
    await asyncio.sleep(0)

    task.cancel()

    with pytest.raises(asyncio.CancelledError):
        await task
    assert fake.close_calls == 1


@_async_test
async def test_open_sanitizes_credential_and_readiness_failures(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    profile = _profile(tmp_path)

    def fail_credentials(**unused: object) -> object:
        raise ValueError("secret credential detail")

    monkeypatch.setattr(
        channel_module.grpc,
        "ssl_channel_credentials",
        fail_credentials,
    )
    with pytest.raises(RuntimeHostChannelError) as credential_failure:
        await open_runtime_host_channel(profile)
    assert credential_failure.value.code == "channel-credentials-invalid"
    assert "secret" not in str(credential_failure.value)

    fake = _FakeChannel()
    fake.ready_failure = RuntimeError("secret transport detail")
    _install_fakes(monkeypatch, fake)
    with pytest.raises(RuntimeHostChannelError) as readiness_failure:
        await open_runtime_host_channel(profile)
    assert readiness_failure.value.code == "channel-readiness-failed"
    assert "secret" not in str(readiness_failure.value)
    assert fake.close_calls == 1


@_async_test
async def test_context_and_concurrent_close_close_exactly_once(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    fake = _FakeChannel()
    fake.close_gate = asyncio.Event()
    _install_fakes(monkeypatch, fake)
    opened = await open_runtime_host_channel(_profile(tmp_path))

    first = asyncio.create_task(opened.close())
    second = asyncio.create_task(opened.close())
    await fake.close_started.wait()
    assert fake.close_calls == 1

    fake.close_gate.set()
    await asyncio.gather(first, second)
    await opened.close()
    assert fake.close_calls == 1


@_async_test
async def test_context_manager_closes_channel(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    fake = _FakeChannel()
    _install_fakes(monkeypatch, fake)

    async with await open_runtime_host_channel(_profile(tmp_path)):
        assert fake.close_calls == 0

    assert fake.close_calls == 1


@_async_test
async def test_close_failure_is_sanitized(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    fake = _FakeChannel()
    fake.close_failure = RuntimeError("secret close detail")
    _install_fakes(monkeypatch, fake)
    opened = await open_runtime_host_channel(_profile(tmp_path))

    with pytest.raises(RuntimeHostChannelError) as captured:
        await opened.close()

    assert captured.value.code == "channel-close-failed"
    assert "secret" not in str(captured.value)
    assert fake.close_calls == 1
