from __future__ import annotations

import asyncio
import importlib.util
from pathlib import Path
from types import SimpleNamespace

import pytest

import hase
from hase import (
    EndpointConnectionState,
    MutationFailureClassification,
    PropertyAccessMode,
    PropertyOperationResult,
    PropertyOperationStatus,
    PropertyQuality,
    PropertyTarget,
    PropertyValue,
    RuntimeHostMutationError,
)


EXAMPLE = Path(__file__).parents[1] / "examples" / "write_same_value_property.py"
SPEC = importlib.util.spec_from_file_location("write_same_value_property", EXAMPLE)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class FakeChannel:
    async def __aenter__(self):
        return self

    async def __aexit__(self, exc_type, exc, tb):
        return False


class FakeClient:
    def __init__(
        self,
        snapshot,
        *,
        initial=False,
        confirmed=False,
        reconciled=False,
        write_failure: Exception | None = None,
    ):
        self.snapshot = snapshot
        self.initial = _result(initial)
        self.confirmed = _result(confirmed)
        self.reconciled = _result(reconciled)
        self.write_failure = write_failure
        self.calls: list[tuple] = []

    async def get_snapshot(self):
        self.calls.append(("snapshot",))
        return self.snapshot

    async def read_authoritative_property(self, target):
        self.calls.append(("read", target))
        read_count = sum(1 for call in self.calls if call[0] == "read")
        return self.initial if read_count == 1 else self.reconciled

    async def write_property(self, target, value):
        self.calls.append(("write", target, value))
        if self.write_failure is not None:
            raise self.write_failure
        return self.confirmed


def _result(value):
    return PropertyOperationResult(
        PropertyOperationStatus.SUCCESS,
        PropertyValue(value, PropertyQuality.GOOD, __import__('datetime').datetime.now(__import__('datetime').timezone.utc)),
        None,
    )


def _snapshot(access=PropertyAccessMode.READ_WRITE):
    descriptor = SimpleNamespace(
        property_id="built-in-led-state",
        display_name="Built-in LED State",
        access_mode=access,
        data=SimpleNamespace(),
    )
    instrument = SimpleNamespace(
        instrument_id="arduino-uno-controller-01",
        properties=(descriptor,),
    )
    endpoint = SimpleNamespace(
        endpoint_id="arduino-uno-01",
        attachment_generation="generation-1",
        connection_status=SimpleNamespace(state=EndpointConnectionState.READY),
        descriptor=SimpleNamespace(instruments=(instrument,)),
    )
    return SimpleNamespace(endpoints=(endpoint,))


def _wire(monkeypatch, client):
    registry = SimpleNamespace(
        resolve=lambda target_id: SimpleNamespace(
            display_name="MiniPC Runtime Host",
            profile=object(),
        )
    )
    monkeypatch.setattr(MODULE, "load_automation_target_registry", lambda path: registry)

    async def open_channel(profile):
        return FakeChannel()

    monkeypatch.setattr(MODULE, "open_runtime_host_channel", open_channel)
    monkeypatch.setattr(MODULE, "RuntimeHostClient", lambda channel: client)


def _run(monkeypatch, client):
    _wire(monkeypatch, client)
    return asyncio.run(
        MODULE.write_same_value_property(
            Path("C:/registry.json"),
            "minipc-runtime-host",
            "arduino-uno-01",
            "arduino-uno-controller-01",
            "built-in-led-state",
        )
    )


def test_parser_requires_confirmation():
    with pytest.raises(SystemExit):
        MODULE._parser().parse_args(
            [
                "--registry", "C:/registry.json",
                "--target", "minipc-runtime-host",
                "--endpoint", "arduino-uno-01",
                "--instrument", "arduino-uno-controller-01",
                "--property", "built-in-led-state",
            ]
        )


def test_parser_has_no_value_argument():
    with pytest.raises(SystemExit):
        MODULE._parser().parse_args(
            [
                "--registry", "C:/registry.json",
                "--target", "minipc-runtime-host",
                "--endpoint", "arduino-uno-01",
                "--instrument", "arduino-uno-controller-01",
                "--property", "built-in-led-state",
                "--confirm-same-value-write",
                "--value", "true",
            ]
        )


@pytest.mark.parametrize(
    ("access", "code"),
    [
        (PropertyAccessMode.READ, "property-not-read-write"),
        (PropertyAccessMode.WRITE, "property-not-read-write"),
    ],
)
def test_resolve_requires_read_write(access, code):
    with pytest.raises(MODULE.ExampleWriteError, match=code):
        MODULE.resolve_property(
            _snapshot(access),
            "arduino-uno-01",
            "arduino-uno-controller-01",
            "built-in-led-state",
        )


def test_resolve_uses_current_attachment_generation():
    target, _ = MODULE.resolve_property(
        _snapshot(),
        "arduino-uno-01",
        "arduino-uno-controller-01",
        "built-in-led-state",
    )
    assert target == PropertyTarget(
        "arduino-uno-01",
        "generation-1",
        "arduino-uno-controller-01",
        "built-in-led-state",
    )


def test_success_uses_one_snapshot_one_write_and_two_reads(monkeypatch):
    client = FakeClient(_snapshot(), initial=False, confirmed=False, reconciled=False)
    output = _run(monkeypatch, client)

    assert [call[0] for call in client.calls] == [
        "snapshot", "read", "write", "read"
    ]
    assert client.calls[2][2] is False
    assert "Write: confirmed" in output
    assert "Reconciliation: matched" in output


@pytest.mark.parametrize("value", [False, True, "CC", 1.25, b"\x00\xff"])
def test_writes_exact_authoritative_value_without_reconstruction(monkeypatch, value):
    client = FakeClient(_snapshot(), initial=value, confirmed=value, reconciled=value)
    _run(monkeypatch, client)
    assert client.calls[2][2] == value
    assert type(client.calls[2][2]) is type(value)


def test_write_confirmation_mismatch_stops_before_reconciliation(monkeypatch):
    client = FakeClient(_snapshot(), initial=False, confirmed=True, reconciled=False)
    with pytest.raises(MODULE.ExampleWriteError, match="write-confirmation-mismatch"):
        _run(monkeypatch, client)
    assert [call[0] for call in client.calls] == ["snapshot", "read", "write"]


def test_reconciliation_mismatch_fails_after_one_write(monkeypatch):
    client = FakeClient(_snapshot(), initial=False, confirmed=False, reconciled=True)
    with pytest.raises(MODULE.ExampleWriteError, match="write-reconciliation-mismatch"):
        _run(monkeypatch, client)
    assert [call[0] for call in client.calls] == [
        "snapshot", "read", "write", "read"
    ]


@pytest.mark.parametrize(
    "classification",
    [
        MutationFailureClassification.REJECTED,
        MutationFailureClassification.OUTCOME_UNCERTAIN,
    ],
)
def test_mutation_failure_never_retries_or_reconciles(monkeypatch, classification):
    client = FakeClient(
        _snapshot(),
        initial=False,
        write_failure=RuntimeHostMutationError("failure", classification),
    )
    with pytest.raises(RuntimeHostMutationError):
        _run(monkeypatch, client)
    assert [call[0] for call in client.calls] == ["snapshot", "read", "write"]


def test_source_has_no_cached_command_observation_diagnostics_or_second_snapshot():
    source = EXAMPLE.read_text(encoding="utf-8")
    assert "read_cached_property" not in source
    assert "execute_command" not in source
    assert ".observe(" not in source
    assert "observe_diagnostics" not in source
    assert "retry" not in source.lower()
    assert source.count("get_snapshot()") == 1
    assert source.count("write_property(") == 1
