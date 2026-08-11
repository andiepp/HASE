from __future__ import annotations

import asyncio
from dataclasses import dataclass
from importlib.util import module_from_spec, spec_from_file_location
from pathlib import Path
import sys

import pytest

from hase import (
    BooleanDataDescriptor,
    CommandDescriptor,
    EndpointConnectionState,
    EndpointConnectionStatus,
    EndpointDescriptor,
    EventDescriptor,
    InstrumentDescriptor,
    NumericDataDescriptor,
    PropertyAccessMode,
    PropertyDescriptor,
    Quantity,
    RuntimeEndpointSnapshot,
    RuntimeHostApiVersion,
    RuntimeHostClientError,
    RuntimeHostProfile,
    RuntimeHostSnapshot,
    Unit,
    ValueRange,
)

EXAMPLE_PATH = Path(__file__).parents[1] / "examples" / "inspect_runtime_host.py"
SPEC = spec_from_file_location("hase_example_inspect_runtime_host", EXAMPLE_PATH)
assert SPEC is not None and SPEC.loader is not None
example = module_from_spec(SPEC)
sys.modules[SPEC.name] = example
SPEC.loader.exec_module(example)


def _snapshot() -> RuntimeHostSnapshot:
    voltage_quantity = Quantity("voltage", "Voltage")
    voltage = NumericDataDescriptor(
        voltage_quantity,
        Unit("volt", "Volt", "V", voltage_quantity),
        ValueRange(0.0, 5.0),
        0.001,
    )
    instrument_b = InstrumentDescriptor(
        "instrument-b",
        "Instrument B",
        "sensor",
        "Example",
        "B",
        "SERIAL-B-MUST-NOT-PRINT",
        "1.0",
        None,
        None,
        (
            PropertyDescriptor(
                "enabled",
                ("State", "Enabled"),
                "Enabled",
                None,
                PropertyAccessMode.READ,
                BooleanDataDescriptor(),
            ),
        ),
        (),
        (),
    )
    instrument_a = InstrumentDescriptor(
        "instrument-a",
        "Instrument A",
        "sensor",
        "Example",
        "A",
        "SERIAL-A-MUST-NOT-PRINT",
        "2.0",
        None,
        None,
        (
            PropertyDescriptor(
                "voltage",
                ("Measurement", "Voltage"),
                "Voltage",
                None,
                PropertyAccessMode.READ,
                voltage,
            ),
        ),
        (CommandDescriptor(("Reset",), "Reset", None, None),),
        (EventDescriptor(("Changed",), "Changed", None, None),),
    )
    endpoint = RuntimeEndpointSnapshot(
        "endpoint-01",
        "GENERATION-MUST-NOT-PRINT",
        EndpointDescriptor(
            "endpoint-01",
            "Endpoint",
            None,
            (instrument_b, instrument_a),
        ),
        EndpointConnectionStatus(EndpointConnectionState.READY, None, None),
    )
    return RuntimeHostSnapshot(
        "HOST-ID-MUST-NOT-PRINT",
        RuntimeHostApiVersion(1, 0),
        (endpoint,),
    )


def test_format_snapshot_is_deterministic_descriptor_only_and_sanitized() -> None:
    output = example.format_snapshot("Desktop Runtime Host", _snapshot())

    assert output.startswith("Target: Desktop Runtime Host\nAPI: 1.0\n")
    assert "Endpoint: endpoint-01" in output
    assert "State: ready" in output
    assert output.index("Instrument: Instrument A") < output.index(
        "Instrument: Instrument B"
    )
    assert "Measurement/Voltage" in output
    assert "Voltage (read)" in output
    assert "quantity=Voltage" in output
    assert "unit=V" in output
    assert "range=0..5" in output
    assert "resolution=0.001" in output
    assert "Reset" in output
    assert "Changed" in output
    assert "SERIAL-A-MUST-NOT-PRINT" not in output
    assert "SERIAL-B-MUST-NOT-PRINT" not in output
    assert "GENERATION-MUST-NOT-PRINT" not in output
    assert "HOST-ID-MUST-NOT-PRINT" not in output


def test_parser_requires_registry_and_target() -> None:
    parser = example._parser()

    with pytest.raises(SystemExit):
        parser.parse_args([])
    with pytest.raises(SystemExit):
        parser.parse_args(["--registry", r"C:\external\targets.json"])
    with pytest.raises(SystemExit):
        parser.parse_args(["--target", "desktop-runtime-host"])


def test_parser_rejects_unknown_target_locally() -> None:
    with pytest.raises(SystemExit):
        example._parser().parse_args(
            [
                "--registry",
                r"C:\external\targets.json",
                "--target",
                "automatic",
            ]
        )


@dataclass
class _Target:
    display_name: str
    profile: RuntimeHostProfile


class _Registry:
    def __init__(self, target: _Target) -> None:
        self.target = target
        self.resolved: list[str] = []

    def resolve(self, target_id: str) -> _Target:
        self.resolved.append(target_id)
        return self.target


class _Channel:
    def __init__(self) -> None:
        self.entered = 0
        self.exited = 0

    async def __aenter__(self) -> "_Channel":
        self.entered += 1
        return self

    async def __aexit__(self, *unused: object) -> None:
        self.exited += 1


class _Client:
    instances: list["_Client"] = []
    failure: Exception | None = None

    def __init__(self, channel: object) -> None:
        self.channel = channel
        self.snapshot_calls = 0
        self.__class__.instances.append(self)

    async def get_snapshot(self) -> RuntimeHostSnapshot:
        self.snapshot_calls += 1
        if self.failure is not None:
            raise self.failure
        return _snapshot()


def _profile(tmp_path: Path) -> RuntimeHostProfile:
    certificate = tmp_path / "client.pem"
    key = tmp_path / "client.key"
    server = tmp_path / "server.cer"
    for path in (certificate, key, server):
        path.write_bytes(b"not-read-by-example-test")
    return RuntimeHostProfile(
        1,
        "https://192.0.2.10:50443",
        certificate,
        key,
        server,
    )


def test_inspect_uses_one_selected_profile_one_snapshot_and_closes(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    target = _Target("Desktop Runtime Host", _profile(tmp_path))
    registry = _Registry(target)
    channel = _Channel()
    opened_profiles: list[RuntimeHostProfile] = []

    def fake_load(path: Path) -> _Registry:
        assert path == tmp_path / "targets.json"
        return registry

    async def fake_open(profile: RuntimeHostProfile) -> _Channel:
        opened_profiles.append(profile)
        return channel

    _Client.instances.clear()
    _Client.failure = None
    monkeypatch.setattr(example, "load_automation_target_registry", fake_load)
    monkeypatch.setattr(example, "open_runtime_host_channel", fake_open)
    monkeypatch.setattr(example, "RuntimeHostClient", _Client)

    output = asyncio.run(
        example.inspect_runtime_host(
            tmp_path / "targets.json",
            "desktop-runtime-host",
        )
    )

    assert registry.resolved == ["desktop-runtime-host"]
    assert opened_profiles == [target.profile]
    assert len(_Client.instances) == 1
    assert _Client.instances[0].snapshot_calls == 1
    assert channel.entered == 1
    assert channel.exited == 1
    assert "Target: Desktop Runtime Host" in output


def test_channel_closes_when_snapshot_fails(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    target = _Target("Desktop Runtime Host", _profile(tmp_path))
    registry = _Registry(target)
    channel = _Channel()

    monkeypatch.setattr(
        example,
        "load_automation_target_registry",
        lambda unused: registry,
    )

    async def fake_open(unused: RuntimeHostProfile) -> _Channel:
        return channel

    _Client.instances.clear()
    _Client.failure = RuntimeHostClientError("rpc-unavailable")
    monkeypatch.setattr(example, "open_runtime_host_channel", fake_open)
    monkeypatch.setattr(example, "RuntimeHostClient", _Client)

    with pytest.raises(RuntimeHostClientError) as captured:
        asyncio.run(
            example.inspect_runtime_host(
                tmp_path / "targets.json",
                "desktop-runtime-host",
            )
        )

    _Client.failure = None
    assert captured.value.code == "rpc-unavailable"
    assert len(_Client.instances) == 1
    assert _Client.instances[0].snapshot_calls == 1
    assert channel.entered == 1
    assert channel.exited == 1


def test_example_source_uses_only_public_hase_api_and_snapshot_operation() -> None:
    source = EXAMPLE_PATH.read_text(encoding="utf-8")

    assert "from hase import (" in source
    assert "hase._" not in source
    assert source.count(".get_snapshot()") == 1
    for forbidden in (
        ".read_authoritative_property(",
        ".read_cached_property(",
        ".write_property(",
        ".execute_command(",
        ".observe(",
        ".observe_diagnostics(",
    ):
        assert forbidden not in source
