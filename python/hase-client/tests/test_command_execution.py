import asyncio
from functools import wraps
from typing import Any

import grpc
import pytest

import hase.client as client_module
from hase import CommandOperationStatus, CommandProjectionError, CommandTarget
from hase import MutationFailureClassification, RuntimeHostChannel
from hase import RuntimeHostClient, RuntimeHostMutationError
from hase._generated import runtime_host_remote_api_v1_pb2 as contract


def async_test(function):
    @wraps(function)
    def run(*args, **kwargs):
        return asyncio.run(function(*args, **kwargs))
    return run


class Channel:
    async def close(self): pass


class Call:
    def __init__(self, result):
        self.result = result
        self.calls = []
    async def __call__(self, request, *, timeout):
        self.calls.append((request, timeout))
        if isinstance(self.result, BaseException): raise self.result
        return self.result


class Stub:
    def __init__(self, call): self.ExecuteCommand = call


def client(monkeypatch, call):
    monkeypatch.setattr(client_module.services, "RuntimeHostRemoteApiStub",
        lambda channel: Stub(call))
    return RuntimeHostClient(RuntimeHostChannel(Channel()))  # type: ignore[arg-type]


def target():
    return CommandTarget("endpoint-01", "generation-1", "instrument-01",
        ("Mode", "SelectCc"))


def success(value=None):
    result = contract.CommandOperationResult(
        status=contract.COMMAND_OPERATION_STATUS_SUCCESS)
    if value is not None: result.return_value.string_value = value
    return result


@async_test
async def test_execute_command_sends_exact_request_once(monkeypatch):
    call = Call(success("CC"))
    result = await client(monkeypatch, call).execute_command(
        target(), True, timeout=3.5)
    assert result.status is CommandOperationStatus.SUCCESS
    assert result.return_value == "CC"
    assert len(call.calls) == 1
    request, timeout = call.calls[0]
    assert request.target.endpoint_id == "endpoint-01"
    assert tuple(request.target.command_path_segments) == ("Mode", "SelectCc")
    assert request.argument.boolean_value is True
    assert timeout == 3.5


@pytest.mark.parametrize("argument", [None, True, "CC", 0.1, 1, b"x"])
@async_test
async def test_execute_command_supports_all_mutation_values(monkeypatch, argument):
    call = Call(success())
    await client(monkeypatch, call).execute_command(target(), argument)
    assert len(call.calls) == 1


@pytest.mark.parametrize("bad", [[], bytearray(b"x")])
@async_test
async def test_execute_command_rejects_bad_argument_before_send(monkeypatch, bad):
    call = Call(success())
    with pytest.raises(RuntimeHostMutationError) as captured:
        await client(monkeypatch, call).execute_command(target(), bad)
    assert captured.value.classification is MutationFailureClassification.NOT_SENT
    assert call.calls == []


@pytest.mark.parametrize("status,code,classification", [
    (contract.COMMAND_OPERATION_STATUS_COMMAND_NOT_FOUND,
        "mutation-command-not-found", MutationFailureClassification.REJECTED),
    (contract.COMMAND_OPERATION_STATUS_ARGUMENT_NOT_SUPPORTED,
        "mutation-command-argument-not-supported", MutationFailureClassification.REJECTED),
    (contract.COMMAND_OPERATION_STATUS_ENDPOINT_FAILURE,
        "mutation-command-endpoint-failure", MutationFailureClassification.OUTCOME_UNCERTAIN),
    (contract.COMMAND_OPERATION_STATUS_TIMED_OUT,
        "mutation-command-timed-out", MutationFailureClassification.OUTCOME_UNCERTAIN),
])
@async_test
async def test_execute_command_maps_status(monkeypatch, status, code, classification):
    response = contract.CommandOperationResult(status=status)
    response.diagnostic = "secret"
    with pytest.raises(RuntimeHostMutationError) as captured:
        await client(monkeypatch, Call(response)).execute_command(target(), True)
    assert captured.value.code == code
    assert captured.value.classification is classification
    assert "secret" not in str(captured.value)


def test_command_target_is_strict():
    with pytest.raises(CommandProjectionError):
        CommandTarget("kel", "generation", "instrument", ())


def test_command_success_rejects_diagnostic():
    response = success()
    response.diagnostic = "secret"
    from hase import project_command_operation_result
    with pytest.raises(CommandProjectionError):
        project_command_operation_result(response)
