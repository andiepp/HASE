from __future__ import annotations

import asyncio
from datetime import datetime, timedelta, timezone
from importlib.util import module_from_spec, spec_from_file_location
from pathlib import Path
import sys

import pytest

from hase import (
    AttachmentEnded,
    AttachmentPublished,
    ConnectionStatusChanged,
    EndpointConnectionState,
    EndpointConnectionStatus,
    EndpointDescriptor,
    EventOccurred,
    InstrumentDescriptor,
    ObservationInitialSnapshot,
    ObservationKind,
    PropertyQuality,
    PropertyValue,
    PropertyValueChanged,
    RuntimeEndpointSnapshot,
    RuntimeHostApiVersion,
    RuntimeHostObservation,
    RuntimeHostProfile,
    RuntimeHostSnapshot,
)

PATH = Path(__file__).parents[1] / "examples" / "observe_runtime_host.py"
SPEC = spec_from_file_location("hase_example_observe_runtime_host", PATH)
assert SPEC is not None and SPEC.loader is not None
example = module_from_spec(SPEC)
sys.modules[SPEC.name] = example
SPEC.loader.exec_module(example)


@pytest.mark.parametrize("value", ["0", "1001", "-1", "abc"])
def test_count_rejects_out_of_range_or_invalid(value: str) -> None:
    with pytest.raises(Exception):
        example._bounded_count(value)


def test_parser_requires_explicit_target_registry_and_count() -> None:
    with pytest.raises(SystemExit):
        example._parser().parse_args([])


def _status(state: EndpointConnectionState) -> EndpointConnectionStatus:
    return EndpointConnectionStatus(state, None, None)


def _endpoint() -> RuntimeEndpointSnapshot:
    instrument = InstrumentDescriptor(
        "arduino-uno-controller-01",
        "Arduino Uno GPIO Controller",
        "controller",
        "Arduino",
        "Uno",
        None,
        None,
        None,
        None,
        (),
        (),
        (),
    )
    return RuntimeEndpointSnapshot(
        "arduino-uno-01",
        "generation-hidden",
        EndpointDescriptor("arduino-uno-01", None, None, (instrument,)),
        _status(EndpointConnectionState.READY),
    )


def _snapshot() -> RuntimeHostSnapshot:
    return RuntimeHostSnapshot(
        "host-hidden",
        RuntimeHostApiVersion(1, 0),
        (_endpoint(),),
    )


def _observation(kind: ObservationKind, payload: object, sequence: int = 42):
    return RuntimeHostObservation(
        sequence,
        "arduino-uno-01",
        "generation-hidden",
        kind,
        payload,
    )


def test_formats_attachment_published_without_generation() -> None:
    text = example.format_observation(
        _observation(
            ObservationKind.ATTACHMENT_PUBLISHED,
            AttachmentPublished(_endpoint()),
        )
    )
    assert "Kind: attachment-published" in text
    assert "Published state: ready" in text
    assert "Instruments: 1" in text
    assert "generation-hidden" not in text


def test_formats_attachment_ended() -> None:
    timestamp = datetime(2026, 8, 11, 7, 30, tzinfo=timezone.utc)
    text = example.format_observation(
        _observation(
            ObservationKind.ATTACHMENT_ENDED,
            AttachmentEnded(timestamp),
        )
    )
    assert timestamp.isoformat() in text


def test_formats_connection_status_changed() -> None:
    text = example.format_observation(
        _observation(
            ObservationKind.CONNECTION_STATUS_CHANGED,
            ConnectionStatusChanged(
                _status(EndpointConnectionState.READY),
                _status(EndpointConnectionState.RECONNECTING),
            ),
        )
    )
    assert "Previous: ready" in text
    assert "Current: reconnecting" in text


def test_formats_property_value_changed() -> None:
    old = PropertyValue(
        2.5,
        datetime(2026, 8, 11, 7, 30, tzinfo=timezone.utc),
        PropertyQuality.GOOD,
    )
    new = PropertyValue(
        2.6,
        datetime(2026, 8, 11, 7, 31, tzinfo=timezone.utc),
        PropertyQuality.GOOD,
    )
    text = example.format_observation(
        _observation(
            ObservationKind.PROPERTY_VALUE_CHANGED,
            PropertyValueChanged(
                "arduino-uno-controller-01",
                "analog-input-voltage",
                old,
                new,
            ),
        )
    )
    assert "Property: analog-input-voltage" in text
    assert "Previous value: 2.5" in text
    assert "Current value: 2.6" in text
    assert "quality=good" in text


def test_formats_event_occurred_with_absent_value() -> None:
    timestamp = datetime(2026, 8, 11, 7, 32, tzinfo=timezone.utc)
    text = example.format_observation(
        _observation(
            ObservationKind.EVENT_OCCURRED,
            EventOccurred(
                "arduino-uno-controller-01",
                ("Controller", "ButtonPressed"),
                timestamp,
                None,
            ),
        )
    )
    assert "Kind: event-occurred" in text
    assert "Event: Controller/ButtonPressed" in text
    assert f"Occurred UTC: {timestamp.isoformat()}" in text
    assert "Value: <absent>" in text


class _Target:
    display_name = "MiniPC Runtime Host"

    def __init__(self, profile: RuntimeHostProfile) -> None:
        self.profile = profile


class _Registry:
    def __init__(self, target: _Target) -> None:
        self.target = target
        self.resolve_calls: list[str] = []

    def resolve(self, target_id: str) -> _Target:
        self.resolve_calls.append(target_id)
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
    messages: tuple[object, ...] = ()
    instances: list["_Client"] = []

    def __init__(self, unused: object) -> None:
        self.observe_calls = 0
        self.yielded = 0
        self.__class__.instances.append(self)

    async def observe(self):
        self.observe_calls += 1
        for item in self.messages:
            self.yielded += 1
            yield item


def _profile(tmp_path: Path) -> RuntimeHostProfile:
    paths = [tmp_path / name for name in ("client.pem", "client.key", "server.cer")]
    for path in paths:
        path.write_bytes(b"not-read")
    return RuntimeHostProfile(
        1,
        "https://192.0.2.11:50443",
        *paths,
    )


def _live(sequence: int) -> RuntimeHostObservation:
    occurred_at = (
        datetime(2026, 8, 11, 7, 32, tzinfo=timezone.utc)
        + timedelta(seconds=sequence)
    )
    return _observation(
        ObservationKind.EVENT_OCCURRED,
        EventOccurred(
            "arduino-uno-controller-01",
            ("Controller", "ButtonPressed"),
            occurred_at,
            None,
        ),
        sequence,
    )


def _wire(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    messages: tuple[object, ...],
):
    registry = _Registry(_Target(_profile(tmp_path)))
    channel = _Channel()
    _Client.instances.clear()
    _Client.messages = messages

    monkeypatch.setattr(
        example,
        "load_automation_target_registry",
        lambda unused: registry,
    )

    async def fake_open(unused: RuntimeHostProfile) -> _Channel:
        return channel

    monkeypatch.setattr(example, "open_runtime_host_channel", fake_open)
    monkeypatch.setattr(example, "RuntimeHostClient", _Client)
    return registry, channel


def test_initial_snapshot_does_not_consume_live_count(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    messages = (
        ObservationInitialSnapshot(_snapshot(), 100),
        _live(101),
        _live(102),
        _live(103),
    )
    registry, channel = _wire(tmp_path, monkeypatch, messages)

    output = asyncio.run(
        example.observe_runtime_host(
            tmp_path / "targets.json",
            "minipc-runtime-host",
            2,
        )
    )

    assert registry.resolve_calls == ["minipc-runtime-host"]
    assert len(_Client.instances) == 1
    client = _Client.instances[0]
    assert client.observe_calls == 1
    assert client.yielded == 3
    assert "Initial snapshot sequence: 100" in output
    assert "Endpoints: 1" in output
    assert output.count("Kind: event-occurred") == 2
    assert "Sequence: 103" not in output
    assert channel.entered == 1
    assert channel.exited == 1
    assert "192.0.2.11" not in output
    assert "host-hidden" not in output
    assert "generation-hidden" not in output


def test_stream_ending_before_count_fails_and_closes(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    messages = (
        ObservationInitialSnapshot(_snapshot(), 100),
        _live(101),
    )
    unused, channel = _wire(tmp_path, monkeypatch, messages)

    with pytest.raises(example.ExampleObservationError) as captured:
        asyncio.run(
            example.observe_runtime_host(
                tmp_path / "targets.json",
                "minipc-runtime-host",
                2,
            )
        )

    assert captured.value.code == "observation-stream-ended-early"
    assert _Client.instances[0].observe_calls == 1
    assert channel.exited == 1


def test_missing_initial_snapshot_fails_and_closes(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    unused, channel = _wire(tmp_path, monkeypatch, (_live(101),))

    with pytest.raises(example.ExampleObservationError) as captured:
        asyncio.run(
            example.observe_runtime_host(
                tmp_path / "targets.json",
                "minipc-runtime-host",
                1,
            )
        )

    assert captured.value.code == "observation-initial-snapshot-missing"
    assert channel.exited == 1


def test_source_uses_only_observation_stream_not_other_operations() -> None:
    source = PATH.read_text(encoding="utf-8")
    assert "hase._" not in source
    assert source.count(".observe()") == 1
    for forbidden in (
        ".get_snapshot(",
        ".read_authoritative_property(",
        ".read_cached_property(",
        ".write_property(",
        ".execute_command(",
        ".observe_diagnostics(",
        "asyncio.gather(",
    ):
        assert forbidden not in source
