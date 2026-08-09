from pathlib import Path

import pytest

import hase._physical_snapshot_validation as validation_module
from hase import ProfileValidationError
from hase import RuntimeHostApiVersion
from hase import RuntimeHostChannelError
from hase import RuntimeHostClientError
from hase import RuntimeHostProfile
from hase import RuntimeHostSnapshot
from hase import SnapshotProjectionError


class _FakeChannel:
    def __init__(self, close_failure: Exception | None = None) -> None:
        self.close_calls = 0
        self.close_failure = close_failure

    async def close(self) -> None:
        self.close_calls += 1
        if self.close_failure is not None:
            raise self.close_failure


class _FakeClient:
    def __init__(
        self,
        snapshot: RuntimeHostSnapshot | None = None,
        failure: Exception | None = None,
    ) -> None:
        self.snapshot = snapshot or RuntimeHostSnapshot(
            "runtime-host",
            RuntimeHostApiVersion(1, 0),
            (),
        )
        self.failure = failure
        self.calls: list[float] = []

    async def get_snapshot(self, *, timeout: float) -> RuntimeHostSnapshot:
        self.calls.append(timeout)
        if self.failure is not None:
            raise self.failure
        return self.snapshot


def _profile(tmp_path: Path) -> RuntimeHostProfile:
    return RuntimeHostProfile(
        1,
        "https://192.0.2.10:50443",
        tmp_path / "client-chain.pem",
        tmp_path / "client-key.pem",
        tmp_path / "trusted-server.cer",
    )


def _install_success(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    *,
    client: _FakeClient | None = None,
    channel: _FakeChannel | None = None,
) -> tuple[_FakeClient, _FakeChannel, dict[str, object]]:
    profile = _profile(tmp_path)
    selected_client = client or _FakeClient()
    selected_channel = channel or _FakeChannel()
    captured: dict[str, object] = {}

    def load(path: str) -> RuntimeHostProfile:
        captured["profile_path"] = path
        return profile

    async def open_channel(
        supplied: RuntimeHostProfile,
        *,
        readiness_timeout: float,
    ) -> _FakeChannel:
        captured["profile"] = supplied
        captured["readiness_timeout"] = readiness_timeout
        return selected_channel

    def create_client(supplied: object) -> _FakeClient:
        captured["channel"] = supplied
        return selected_client

    monkeypatch.setattr(validation_module, "load_runtime_host_profile", load)
    monkeypatch.setattr(validation_module, "open_runtime_host_channel", open_channel)
    monkeypatch.setattr(validation_module, "RuntimeHostClient", create_client)
    return selected_client, selected_channel, captured


def test_main_gets_one_snapshot_closes_once_and_prints_fixed_output(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    client, channel, captured = _install_success(tmp_path, monkeypatch)
    profile_path = str(tmp_path / "profile.json")

    assert validation_module.main((profile_path,)) == 0

    output = capsys.readouterr()
    assert output.out.splitlines() == [
        "Profile loaded       : True",
        "Channel ready        : True",
        "Snapshot received    : True",
        "Snapshot valid       : True",
        "Channel closed       : True",
        "Validation succeeded : True",
    ]
    assert output.err == ""
    assert client.calls == [10.0]
    assert channel.close_calls == 1
    assert captured == {
        "profile_path": profile_path,
        "profile": _profile(tmp_path),
        "readiness_timeout": 10.0,
        "channel": channel,
    }


@pytest.mark.parametrize(
    ("failure", "code"),
    [
        (RuntimeHostClientError("rpc-permission-denied"), "rpc-permission-denied"),
        (SnapshotProjectionError("snapshot-data-kind-invalid"),
         "snapshot-data-kind-invalid"),
    ],
)
def test_snapshot_failure_closes_once_and_reports_only_code(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
    failure: Exception,
    code: str,
) -> None:
    secret_path = str(tmp_path / "secret-profile.json")
    client = _FakeClient(failure=failure)
    _, channel, _ = _install_success(tmp_path, monkeypatch, client=client)

    assert validation_module.main((secret_path,)) == 1

    output = capsys.readouterr()
    assert output.out == ""
    assert output.err == f"Python physical snapshot validation failed: {code}.\n"
    assert secret_path not in output.err
    assert channel.close_calls == 1
    assert client.calls == [10.0]


def test_unsupported_api_version_closes_and_is_sanitized(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    client = _FakeClient(
        snapshot=RuntimeHostSnapshot(
            "runtime-host",
            RuntimeHostApiVersion(2, 0),
            (),
        )
    )
    _, channel, _ = _install_success(tmp_path, monkeypatch, client=client)

    assert validation_module.main((str(tmp_path / "profile.json"),)) == 1

    output = capsys.readouterr()
    assert output.out == ""
    assert output.err == (
        "Python physical snapshot validation failed: "
        "snapshot-api-version-unsupported.\n"
    )
    assert channel.close_calls == 1


def test_close_failure_is_sanitized(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    channel = _FakeChannel(RuntimeHostChannelError("channel-close-failed"))
    _install_success(tmp_path, monkeypatch, channel=channel)

    assert validation_module.main((str(tmp_path / "profile.json"),)) == 1

    output = capsys.readouterr()
    assert output.out == ""
    assert output.err == (
        "Python physical snapshot validation failed: channel-close-failed.\n"
    )
    assert channel.close_calls == 1


@pytest.mark.parametrize(
    ("failure", "code"),
    [
        (ProfileValidationError("profile-file-unavailable"),
         "profile-file-unavailable"),
        (RuntimeHostChannelError("channel-readiness-timeout"),
         "channel-readiness-timeout"),
    ],
)
def test_pre_channel_failure_reports_only_code(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
    failure: Exception,
    code: str,
) -> None:
    def fail(unused: str) -> RuntimeHostProfile:
        raise failure

    monkeypatch.setattr(validation_module, "load_runtime_host_profile", fail)
    secret_path = str(tmp_path / "secret-profile.json")

    assert validation_module.main((secret_path,)) == 1

    output = capsys.readouterr()
    assert output.out == ""
    assert output.err == f"Python physical snapshot validation failed: {code}.\n"
    assert secret_path not in output.err


def test_main_rejects_arguments_without_echoing_them(
    capsys: pytest.CaptureFixture[str],
) -> None:
    secret = "C:\\secret\\profile.json"

    assert validation_module.main(()) == 1
    assert validation_module.main((secret, "extra")) == 1

    output = capsys.readouterr()
    assert output.out == ""
    assert output.err.splitlines() == [
        "Python physical snapshot validation failed: arguments-invalid.",
        "Python physical snapshot validation failed: arguments-invalid.",
    ]
    assert secret not in output.err


def test_main_sanitizes_unexpected_failure(
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    def fail(unused: str) -> RuntimeHostProfile:
        raise RuntimeError("secret deployment detail")

    monkeypatch.setattr(validation_module, "load_runtime_host_profile", fail)

    assert validation_module.main(("C:\\secret\\profile.json",)) == 1

    output = capsys.readouterr()
    assert output.out == ""
    assert output.err == (
        "Python physical snapshot validation failed: unexpected-failure.\n"
    )
    assert "secret" not in output.err
