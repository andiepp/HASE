from __future__ import annotations

import asyncio
import importlib.util
from pathlib import Path
from types import SimpleNamespace

import pytest

from hase import (
    CommandOperationResult,
    CommandOperationStatus,
    CommandTarget,
    EndpointConnectionState,
    MutationFailureClassification,
    RuntimeHostMutationError,
)

EXAMPLE = Path(__file__).parents[1] / "examples" / "execute_command.py"
SPEC = importlib.util.spec_from_file_location("execute_command", EXAMPLE)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class FakeChannel:
    async def __aenter__(self):
        return self

    async def __aexit__(self, exc_type, exc, tb):
        return False


class FakeClient:
    def __init__(self, snapshot, *, failure=None,
                 status=CommandOperationStatus.SUCCESS):
        self.snapshot = snapshot
        self.failure = failure
        self.status = status
        self.calls = []

    async def get_snapshot(self):
        self.calls.append(("snapshot",))
        return self.snapshot

    async def execute_command(self, target):
        self.calls.append(("command", target))
        if self.failure is not None:
            raise self.failure
        return CommandOperationResult(self.status, None, None)


def _snapshot(*, argument=None, state=EndpointConnectionState.READY):
    command = SimpleNamespace(
        path_segments=("Led", "Toggle"),
        display_name="Toggle LED",
        argument=argument,
    )
    instrument = SimpleNamespace(
        instrument_id="arduino-uno-controller-01",
        commands=(command,),
    )
    endpoint = SimpleNamespace(
        endpoint_id="arduino-uno-01",
        attachment_generation="generation-1",
        connection_status=SimpleNamespace(state=state),
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
        MODULE.execute_parameterless_command(
            Path("C:/registry.json"),
            "minipc-runtime-host",
            "arduino-uno-01",
            "arduino-uno-controller-01",
            ("Led", "Toggle"),
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
                "--command", "Led", "Toggle",
            ]
        )


def test_parser_requires_explicit_command_path():
    with pytest.raises(SystemExit):
        MODULE._parser().parse_args(
            [
                "--registry", "C:/registry.json",
                "--target", "minipc-runtime-host",
                "--endpoint", "arduino-uno-01",
                "--instrument", "arduino-uno-controller-01",
                "--confirm-command-execution",
            ]
        )


def test_resolve_uses_current_attachment_generation():
    target, _ = MODULE.resolve_command(
        _snapshot(),
        "arduino-uno-01",
        "arduino-uno-controller-01",
        ("Led", "Toggle"),
    )
    assert target == CommandTarget(
        "arduino-uno-01",
        "generation-1",
        "arduino-uno-controller-01",
        ("Led", "Toggle"),
    )


def test_resolve_rejects_endpoint_not_ready():
    with pytest.raises(MODULE.ExampleCommandError, match="endpoint-not-ready"):
        MODULE.resolve_command(
            _snapshot(state=EndpointConnectionState.DISCONNECTED),
            "arduino-uno-01",
            "arduino-uno-controller-01",
            ("Led", "Toggle"),
        )


def test_resolve_rejects_argument_command():
    with pytest.raises(
        MODULE.ExampleCommandError,
        match="command-argument-not-supported",
    ):
        MODULE.resolve_command(
            _snapshot(argument=object()),
            "arduino-uno-01",
            "arduino-uno-controller-01",
            ("Led", "Toggle"),
        )


def test_success_uses_one_snapshot_and_one_command(monkeypatch):
    client = FakeClient(_snapshot())
    output = _run(monkeypatch, client)
    assert [call[0] for call in client.calls] == ["snapshot", "command"]
    assert "Command path: Led/Toggle" in output
    assert "Execution: confirmed" in output


@pytest.mark.parametrize(
    "classification",
    [
        MutationFailureClassification.REJECTED,
        MutationFailureClassification.OUTCOME_UNCERTAIN,
    ],
)
def test_mutation_failure_never_retries(monkeypatch, classification):
    client = FakeClient(
        _snapshot(),
        failure=RuntimeHostMutationError("failure", classification),
    )
    with pytest.raises(RuntimeHostMutationError):
        _run(monkeypatch, client)
    assert [call[0] for call in client.calls] == ["snapshot", "command"]


@pytest.mark.parametrize(
    "status",
    [
        CommandOperationStatus.ATTACHMENT_NOT_CURRENT,
        CommandOperationStatus.ENDPOINT_REJECTED,
        CommandOperationStatus.ENDPOINT_FAILURE,
        CommandOperationStatus.TIMED_OUT,
    ],
)
def test_non_success_result_stops_after_one_command(monkeypatch, status):
    client = FakeClient(_snapshot(), status=status)
    with pytest.raises(MODULE.ExampleCommandError):
        _run(monkeypatch, client)
    assert [call[0] for call in client.calls] == ["snapshot", "command"]


def test_source_has_no_retry_reconnect_write_observation_or_diagnostics():
    source = EXAMPLE.read_text(encoding="utf-8")
    assert "write_property(" not in source
    assert ".observe(" not in source
    assert "observe_diagnostics" not in source
    assert "read_cached_property" not in source
    assert "retry" not in source.lower()
    assert "reconnect" not in source.lower()
    assert source.count("get_snapshot()") == 1
    assert source.count("client.execute_command(") == 1
