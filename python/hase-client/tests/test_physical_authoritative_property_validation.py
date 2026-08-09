from dataclasses import replace
from datetime import datetime, timezone
import math
from pathlib import Path

import pytest

import hase._physical_authoritative_property_validation as validation_module
from hase import BooleanDataDescriptor
from hase import EndpointConnectionState
from hase import EndpointConnectionStatus
from hase import EndpointDescriptor
from hase import InstrumentDescriptor
from hase import NumericDataDescriptor
from hase import ProfileValidationError
from hase import PropertyAccessMode
from hase import PropertyDescriptor
from hase import PropertyOperationResult
from hase import PropertyOperationStatus
from hase import PropertyQuality
from hase import PropertyTarget
from hase import PropertyValue
from hase import Quantity
from hase import RuntimeEndpointSnapshot
from hase import RuntimeHostApiVersion
from hase import RuntimeHostChannelError
from hase import RuntimeHostClientError
from hase import RuntimeHostProfile
from hase import RuntimeHostSnapshot
from hase import Unit


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
        snapshot: RuntimeHostSnapshot,
        result: PropertyOperationResult,
        *,
        snapshot_failure: Exception | None = None,
        read_failure: Exception | None = None,
    ) -> None:
        self.snapshot = snapshot
        self.result = result
        self.snapshot_failure = snapshot_failure
        self.read_failure = read_failure
        self.snapshot_calls: list[float] = []
        self.read_calls: list[tuple[PropertyTarget, float]] = []

    async def get_snapshot(self, *, timeout: float) -> RuntimeHostSnapshot:
        self.snapshot_calls.append(timeout)
        if self.snapshot_failure is not None:
            raise self.snapshot_failure
        return self.snapshot

    async def read_authoritative_property(
        self,
        target: PropertyTarget,
        *,
        timeout: float,
    ) -> PropertyOperationResult:
        self.read_calls.append((target, timeout))
        if self.read_failure is not None:
            raise self.read_failure
        return self.result


def _profile(tmp_path: Path) -> RuntimeHostProfile:
    return RuntimeHostProfile(
        1,
        "https://192.0.2.10:50443",
        tmp_path / "client-chain.pem",
        tmp_path / "client-key.pem",
        tmp_path / "trusted-server.cer",
    )


def _numeric_data() -> NumericDataDescriptor:
    quantity = Quantity("electric-voltage", "Electric voltage")
    return NumericDataDescriptor(
        quantity,
        Unit("volt", "Volt", "V", quantity),
        None,
        None,
    )


def _snapshot(
    *,
    state: EndpointConnectionState = EndpointConnectionState.READY,
    access: PropertyAccessMode = PropertyAccessMode.READ,
    numeric: bool = True,
    endpoint_count: int = 1,
) -> RuntimeHostSnapshot:
    endpoints = []
    for index in range(endpoint_count):
        descriptor = PropertyDescriptor(
            "measured-voltage",
            ("Measurement", "Voltage"),
            "Measured voltage",
            None,
            access,
            _numeric_data() if numeric else BooleanDataDescriptor(),
        )
        instrument = InstrumentDescriptor(
            "electronic-load-01",
            "Electronic Load",
            "ElectronicLoad",
            None,
            None,
            None,
            None,
            None,
            None,
            (descriptor,),
            (),
            (),
        )
        endpoint_id = f"kel-endpoint-{index}"
        endpoints.append(
            RuntimeEndpointSnapshot(
                endpoint_id,
                f"attachment-{index}",
                EndpointDescriptor(endpoint_id, None, None, (instrument,)),
                EndpointConnectionStatus(state, None, None),
            )
        )
    return RuntimeHostSnapshot(
        "runtime-host",
        RuntimeHostApiVersion(1, 0),
        tuple(endpoints),
    )


def _result() -> PropertyOperationResult:
    return PropertyOperationResult(
        PropertyOperationStatus.SUCCESS,
        PropertyValue(
            0.0,
            datetime(2026, 8, 9, 10, 11, 12, tzinfo=timezone.utc),
            PropertyQuality.GOOD,
        ),
        None,
    )


def _install(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    *,
    client: _FakeClient | None = None,
    channel: _FakeChannel | None = None,
) -> tuple[_FakeClient, _FakeChannel, dict[str, object]]:
    profile = _profile(tmp_path)
    selected_client = client or _FakeClient(_snapshot(), _result())
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


def test_main_resolves_current_target_reads_once_and_prints_fixed_output(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    client, channel, captured = _install(tmp_path, monkeypatch)
    profile_path = str(tmp_path / "profile.json")

    assert validation_module.main((profile_path,)) == 0

    output = capsys.readouterr()
    assert output.out.splitlines() == [
        "Profile loaded       : True",
        "Channel ready        : True",
        "Target resolved      : True",
        "Read completed       : True",
        "Result valid         : True",
        "Channel closed       : True",
        "Validation succeeded : True",
    ]
    assert output.err == ""
    assert client.snapshot_calls == [10.0]
    assert client.read_calls == [
        (
            PropertyTarget(
                "kel-endpoint-0",
                "attachment-0",
                "electronic-load-01",
                "measured-voltage",
            ),
            10.0,
        )
    ]
    assert channel.close_calls == 1
    assert captured == {
        "profile_path": profile_path,
        "profile": _profile(tmp_path),
        "readiness_timeout": 10.0,
        "channel": channel,
    }


@pytest.mark.parametrize(
    "snapshot",
    [
        _snapshot(endpoint_count=0),
        _snapshot(endpoint_count=2),
        _snapshot(state=EndpointConnectionState.DISCONNECTED),
        _snapshot(access=PropertyAccessMode.WRITE),
        _snapshot(numeric=False),
    ],
)
def test_target_resolution_requires_one_ready_readable_numeric_property(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
    snapshot: RuntimeHostSnapshot,
) -> None:
    client = _FakeClient(snapshot, _result())
    _, channel, _ = _install(tmp_path, monkeypatch, client=client)

    assert validation_module.main((str(tmp_path / "profile.json"),)) == 1

    output = capsys.readouterr()
    assert output.out == ""
    assert output.err == (
        "Python physical Property validation failed: "
        "property-target-not-unique.\n"
    )
    assert client.snapshot_calls == [10.0]
    assert client.read_calls == []
    assert channel.close_calls == 1


@pytest.mark.parametrize(
    "result",
    [
        PropertyOperationResult(
            PropertyOperationStatus.ENDPOINT_FAILURE,
            None,
            "failure",
        ),
        PropertyOperationResult(PropertyOperationStatus.SUCCESS, None, None),
        replace(_result(), diagnostic="unexpected"),
        replace(_result(), confirmed_value=replace(_result().confirmed_value, value=True)),
        replace(_result(), confirmed_value=replace(_result().confirmed_value, value=math.inf)),
        replace(
            _result(),
            confirmed_value=replace(
                _result().confirmed_value,
                quality=PropertyQuality.BAD,
            ),
        ),
        replace(
            _result(),
            confirmed_value=replace(
                _result().confirmed_value,
                timestamp_utc=datetime(2026, 8, 9, 10, 11, 12),
            ),
        ),
    ],
)
def test_result_validation_rejects_non_successful_or_non_good_numeric_value(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
    result: PropertyOperationResult,
) -> None:
    client = _FakeClient(_snapshot(), result)
    _, channel, _ = _install(tmp_path, monkeypatch, client=client)

    assert validation_module.main((str(tmp_path / "profile.json"),)) == 1

    output = capsys.readouterr()
    assert output.out == ""
    assert output.err == (
        "Python physical Property validation failed: property-result-invalid.\n"
    )
    assert len(client.read_calls) == 1
    assert channel.close_calls == 1


@pytest.mark.parametrize(
    "failure",
    [
        RuntimeHostClientError("rpc-permission-denied"),
        RuntimeHostClientError("rpc-deadline-exceeded"),
    ],
)
def test_rpc_failure_is_sanitized_and_closes_once(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
    failure: RuntimeHostClientError,
) -> None:
    client = _FakeClient(_snapshot(), _result(), read_failure=failure)
    _, channel, _ = _install(tmp_path, monkeypatch, client=client)

    assert validation_module.main((str(tmp_path / "secret.json"),)) == 1

    output = capsys.readouterr()
    assert output.out == ""
    assert output.err == (
        f"Python physical Property validation failed: {failure.code}.\n"
    )
    assert "secret" not in output.err
    assert channel.close_calls == 1


def test_close_failure_is_sanitized(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    channel = _FakeChannel(RuntimeHostChannelError("channel-close-failed"))
    _install(tmp_path, monkeypatch, channel=channel)

    assert validation_module.main((str(tmp_path / "profile.json"),)) == 1

    output = capsys.readouterr()
    assert output.out == ""
    assert output.err == (
        "Python physical Property validation failed: channel-close-failed.\n"
    )
    assert channel.close_calls == 1


def test_profile_failure_and_arguments_are_sanitized(
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    def fail(unused: str) -> RuntimeHostProfile:
        raise ProfileValidationError("profile-file-unavailable")

    monkeypatch.setattr(validation_module, "load_runtime_host_profile", fail)
    secret = "C:\\secret\\profile.json"

    assert validation_module.main((secret,)) == 1
    assert validation_module.main(()) == 1
    assert validation_module.main((secret, "extra")) == 1

    output = capsys.readouterr()
    assert output.out == ""
    assert output.err.splitlines() == [
        "Python physical Property validation failed: profile-file-unavailable.",
        "Python physical Property validation failed: arguments-invalid.",
        "Python physical Property validation failed: arguments-invalid.",
    ]
    assert secret not in output.err
