from dataclasses import replace
from datetime import datetime, timezone
from pathlib import Path

import pytest

import hase._physical_minipc_authoritative_property_validation as validation
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
from hase import RuntimeHostClientError
from hase import RuntimeHostProfile
from hase import RuntimeHostSnapshot
from hase import Unit
from hase import ValueRange


class _Channel:
    def __init__(self) -> None:
        self.close_calls = 0

    async def close(self) -> None:
        self.close_calls += 1


class _Client:
    def __init__(
        self,
        snapshot: RuntimeHostSnapshot,
        result: PropertyOperationResult,
        failure: Exception | None = None,
    ) -> None:
        self.snapshot = snapshot
        self.result = result
        self.failure = failure
        self.snapshot_calls: list[float] = []
        self.read_calls: list[tuple[PropertyTarget, float]] = []

    async def get_snapshot(self, *, timeout: float) -> RuntimeHostSnapshot:
        self.snapshot_calls.append(timeout)
        return self.snapshot

    async def read_authoritative_property(
        self,
        target: PropertyTarget,
        *,
        timeout: float,
    ) -> PropertyOperationResult:
        self.read_calls.append((target, timeout))
        if self.failure is not None:
            raise self.failure
        return self.result


def _numeric(*, ranged: bool = True) -> NumericDataDescriptor:
    quantity = Quantity("voltage", "Voltage")
    return NumericDataDescriptor(
        quantity,
        Unit("volt", "Volt", "V", quantity),
        ValueRange(0.0, 5.0) if ranged else None,
        5.0 / 1023.0,
    )


def _snapshot(
    *,
    state: EndpointConnectionState = EndpointConnectionState.READY,
    access: PropertyAccessMode = PropertyAccessMode.READ,
    instrument_id: str = "arduino-uno-controller-01",
    property_id: str = "analog-input-voltage",
    numeric: bool = True,
    ranged: bool = True,
    count: int = 1,
) -> RuntimeHostSnapshot:
    endpoints = []
    for index in range(count):
        descriptor = PropertyDescriptor(
            property_id,
            ("Analog", "Voltage"),
            "Analog Input Voltage",
            None,
            access,
            _numeric(ranged=ranged) if numeric else BooleanDataDescriptor(),
        )
        instrument = InstrumentDescriptor(
            instrument_id,
            "Arduino Uno GPIO Controller",
            "controller",
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
        endpoints.append(
            RuntimeEndpointSnapshot(
                "arduino-uno-01",
                f"generation-{index}",
                EndpointDescriptor("arduino-uno-01", None, None, (instrument,)),
                EndpointConnectionStatus(state, None, None),
            )
        )
    return RuntimeHostSnapshot(
        "minipc-runtime-host",
        RuntimeHostApiVersion(1, 0),
        tuple(endpoints),
    )


def _result(value: object = 2.5) -> PropertyOperationResult:
    return PropertyOperationResult(
        PropertyOperationStatus.SUCCESS,
        PropertyValue(
            value,  # type: ignore[arg-type]
            datetime(2026, 8, 10, 8, 0, tzinfo=timezone.utc),
            PropertyQuality.GOOD,
        ),
        None,
    )


def _install(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    *,
    snapshot: RuntimeHostSnapshot | None = None,
    result: PropertyOperationResult | None = None,
    read_failure: Exception | None = None,
) -> tuple[_Client, _Channel]:
    profile = RuntimeHostProfile(
        1,
        "https://192.0.2.10:50443",
        tmp_path / "certificate.pem",
        tmp_path / "key.pem",
        tmp_path / "server.cer",
    )
    channel = _Channel()
    client = _Client(snapshot or _snapshot(), result or _result(), read_failure)
    monkeypatch.setattr(validation, "load_runtime_host_profile", lambda _: profile)

    async def open_channel(
        supplied: RuntimeHostProfile,
        *,
        readiness_timeout: float,
    ) -> _Channel:
        assert supplied == profile
        assert readiness_timeout == 10.0
        return channel

    monkeypatch.setattr(validation, "open_runtime_host_channel", open_channel)
    monkeypatch.setattr(validation, "RuntimeHostClient", lambda _: client)
    return client, channel


def test_main_reads_current_a0_target_once_and_prints_fixed_output(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    client, channel = _install(tmp_path, monkeypatch)

    assert validation.main((str(tmp_path / "profile.json"),)) == 0

    output = capsys.readouterr()
    assert output.out.splitlines() == [
        "Profile loaded       : True",
        "Channel ready        : True",
        "A0 target resolved   : True",
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
                "arduino-uno-01",
                "generation-0",
                "arduino-uno-controller-01",
                "analog-input-voltage",
            ),
            10.0,
        )
    ]
    assert channel.close_calls == 1


@pytest.mark.parametrize(
    "snapshot",
    [
        _snapshot(count=0),
        _snapshot(count=2),
        _snapshot(state=EndpointConnectionState.DISCONNECTED),
        _snapshot(access=PropertyAccessMode.WRITE),
        _snapshot(instrument_id="other"),
        _snapshot(property_id="other"),
        _snapshot(numeric=False),
        _snapshot(ranged=False),
    ],
)
def test_main_rejects_non_unique_readable_ranged_a0_target(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
    snapshot: RuntimeHostSnapshot,
) -> None:
    client, channel = _install(tmp_path, monkeypatch, snapshot=snapshot)

    assert validation.main((str(tmp_path / "profile.json"),)) == 1

    output = capsys.readouterr()
    assert output.out == ""
    assert output.err.endswith("property-target-not-unique.\n")
    assert client.read_calls == []
    assert channel.close_calls == 1


@pytest.mark.parametrize("value", [-0.001, 5.001, float("nan"), True])
def test_main_rejects_invalid_or_out_of_range_result(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
    value: object,
) -> None:
    _, channel = _install(tmp_path, monkeypatch, result=_result(value))

    assert validation.main((str(tmp_path / "profile.json"),)) == 1

    assert capsys.readouterr().err.endswith("property-result-invalid.\n")
    assert channel.close_calls == 1


def test_main_rejects_invalid_result_shape(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    result = replace(_result(), status=PropertyOperationStatus.ENDPOINT_REJECTED)
    _, channel = _install(tmp_path, monkeypatch, result=result)

    assert validation.main((str(tmp_path / "profile.json"),)) == 1

    assert capsys.readouterr().err.endswith("property-result-invalid.\n")
    assert channel.close_calls == 1


def test_main_sanitizes_known_failure_and_closes_channel(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    _, channel = _install(
        tmp_path,
        monkeypatch,
        read_failure=RuntimeHostClientError("rpc-authorization-failed"),
    )

    assert validation.main((str(tmp_path / "secret-profile.json"),)) == 1

    output = capsys.readouterr()
    assert output.err.endswith("rpc-authorization-failed.\n")
    assert "secret-profile" not in output.err
    assert channel.close_calls == 1


def test_main_rejects_arguments_before_loading_profile(
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    monkeypatch.setattr(
        validation,
        "load_runtime_host_profile",
        lambda _: (_ for _ in ()).throw(AssertionError()),
    )

    assert validation.main(()) == 1
    assert capsys.readouterr().err.endswith("arguments-invalid.\n")


def test_main_sanitizes_profile_and_unexpected_failures(
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    monkeypatch.setattr(
        validation,
        "load_runtime_host_profile",
        lambda _: (_ for _ in ()).throw(ProfileValidationError("profile-invalid")),
    )
    assert validation.main(("profile.json",)) == 1
    assert capsys.readouterr().err.endswith("profile-invalid.\n")

    monkeypatch.setattr(
        validation,
        "load_runtime_host_profile",
        lambda _: (_ for _ in ()).throw(ValueError("sensitive")),
    )
    assert validation.main(("profile.json",)) == 1
    assert capsys.readouterr().err.endswith("unexpected-failure.\n")


def test_powershell_tool_uses_only_repository_local_environment() -> None:
    package_directory = Path(__file__).resolve().parents[1]
    tool = (
        package_directory
        / "tools"
        / "Test-HaseMiniPcPythonAuthoritativeProperty.ps1"
    ).read_text(encoding="utf-8")

    assert 'Set-StrictMode -Version Latest' in tool
    assert '".venv\\Scripts\\python.exe"' in tool
    assert "-m hase._physical_minipc_authoritative_property_validation" in tool
    assert "ProfilePath" in tool


def test_powershell_tool_contains_no_mutating_workflow() -> None:
    package_directory = Path(__file__).resolve().parents[1]
    tool = (
        package_directory
        / "tools"
        / "Test-HaseMiniPcPythonAuthoritativeProperty.ps1"
    ).read_text(encoding="utf-8")

    assert "WriteProperty" not in tool
    assert "ExecuteCommand" not in tool
    assert "Enable-Hase" not in tool
