from datetime import datetime, timezone
from pathlib import Path

import pytest

import hase._physical_property_write_validation as validation
from hase import (
    BooleanDataDescriptor,
    EndpointConnectionState,
    EndpointConnectionStatus,
    EndpointDescriptor,
    InstrumentDescriptor,
    NumericDataDescriptor,
    PropertyAccessMode,
    PropertyDescriptor,
    PropertyOperationResult,
    PropertyOperationStatus,
    PropertyQuality,
    PropertyValue,
    Quantity,
    RuntimeEndpointSnapshot,
    RuntimeHostApiVersion,
    RuntimeHostProfile,
    RuntimeHostSnapshot,
    StringDataDescriptor,
    Unit,
)


def _snapshot() -> RuntimeHostSnapshot:
    quantity = Quantity("electric-current", "Electric current")
    numeric = NumericDataDescriptor(
        quantity, Unit("ampere", "Ampere", "A", quantity), None, None)
    properties = (
        PropertyDescriptor("operating-mode", ("Operating", "Mode"),
            "Operating mode", None, PropertyAccessMode.READ,
            StringDataDescriptor()),
        PropertyDescriptor("input-enabled", ("Input", "Enabled"),
            "Input enabled", None, PropertyAccessMode.READ,
            BooleanDataDescriptor()),
        PropertyDescriptor("target-current", ("Target", "Current"),
            "Target current", None, PropertyAccessMode.READ_WRITE, numeric),
    )
    instrument = InstrumentDescriptor("electronic-load-01", "Electronic Load",
        "ElectronicLoad", None, None, None, None, None, None,
        properties, (), ())
    endpoint = RuntimeEndpointSnapshot("kel-103", "generation-7",
        EndpointDescriptor("kel-103", None, None, (instrument,)),
        EndpointConnectionStatus(EndpointConnectionState.READY, None, None))
    return RuntimeHostSnapshot("host", RuntimeHostApiVersion(1, 0), (endpoint,))


def _result(value: object) -> PropertyOperationResult:
    return PropertyOperationResult(PropertyOperationStatus.SUCCESS,
        PropertyValue(value, datetime(2026, 8, 9, 10, 11, 12,
            tzinfo=timezone.utc), PropertyQuality.GOOD), None)


class _Channel:
    def __init__(self) -> None:
        self.close_calls = 0

    async def close(self) -> None:
        self.close_calls += 1


class _Client:
    def __init__(self, *, unsafe: bool = False) -> None:
        self.unsafe = unsafe
        self.read_calls = []
        self.write_calls = []

    async def get_snapshot(self, *, timeout: float):
        return _snapshot()

    async def read_authoritative_property(self, target, *, timeout: float):
        self.read_calls.append(target.property_id)
        if target.property_id == "operating-mode":
            return _result("CV" if self.unsafe else "CC")
        if target.property_id == "input-enabled":
            return _result(False)
        return _result(0.1)

    async def write_property(self, target, value, *, timeout: float):
        self.write_calls.append((target.property_id, value, timeout))
        return _result(value)


def _install(tmp_path: Path, monkeypatch: pytest.MonkeyPatch, client: _Client):
    profile = RuntimeHostProfile(1, "https://192.0.2.10:5443",
        tmp_path / "chain.pem", tmp_path / "key.pem", tmp_path / "server.cer")
    channel = _Channel()
    monkeypatch.setattr(validation, "load_runtime_host_profile", lambda path: profile)
    async def open_channel(value, *, readiness_timeout):
        return channel
    monkeypatch.setattr(validation, "open_runtime_host_channel", open_channel)
    monkeypatch.setattr(validation, "RuntimeHostClient", lambda value: client)
    return channel


def test_validate_mode_writes_same_value_once_and_reconciles(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    client = _Client()
    channel = _install(tmp_path, monkeypatch, client)

    assert validation.main((str(tmp_path / "profile.json"),)) == 0

    assert client.read_calls == ["operating-mode", "input-enabled",
        "target-current", "target-current"]
    assert client.write_calls == [("target-current", 0.1, 10.0)]
    assert channel.close_calls == 1
    assert "Validation succeeded        : True" in capsys.readouterr().out


def test_unsafe_mode_stops_before_write_and_closes(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    client = _Client(unsafe=True)
    channel = _install(tmp_path, monkeypatch, client)

    assert validation.main((str(tmp_path / "profile.json"),)) == 1

    assert client.write_calls == []
    assert channel.close_calls == 1
    assert "kel103-state-not-safe" in capsys.readouterr().err


def test_target_resolution_requires_exact_ready_descriptor_set() -> None:
    targets = validation._resolve_targets(_snapshot())
    assert tuple(target.property_id for target in targets) == (
        "operating-mode", "input-enabled", "target-current")


@pytest.mark.parametrize("arguments", [(),
    ("profile", "other"), ("one", "validate", "extra")])
def test_arguments_are_rejected(arguments, capsys: pytest.CaptureFixture[str]) -> None:
    assert validation.main(arguments) == 1
    assert "arguments-invalid" in capsys.readouterr().err
