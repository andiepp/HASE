import asyncio
from functools import wraps
import math
from typing import Any, Callable, Coroutine, TypeVar

import grpc
import pytest

import hase.client as client_module
from hase import MutationFailureClassification
from hase import PropertyOperationStatus
from hase import PropertyTarget
from hase import RuntimeHostChannel
from hase import RuntimeHostClient
from hase import RuntimeHostMutationError
from hase._generated import runtime_host_remote_api_v1_pb2 as contract


_T = TypeVar("_T")


def _async_test(
    function: Callable[..., Coroutine[Any, Any, _T]],
) -> Callable[..., _T]:
    @wraps(function)
    def run(*args: object, **kwargs: object) -> _T:
        return asyncio.run(function(*args, **kwargs))

    return run


class _UnderlyingChannel:
    async def close(self) -> None:
        return None


class _RpcFailure(grpc.RpcError):
    def __init__(self, status: grpc.StatusCode) -> None:
        super().__init__()
        self._status = status

    def code(self) -> grpc.StatusCode:
        return self._status

    def details(self) -> str:
        return "secret server, credential, and endpoint details"


class _WriteCall:
    def __init__(self, response: object) -> None:
        self.response = response
        self.calls: list[tuple[object, float]] = []
        self.gate: asyncio.Event | None = None

    async def __call__(self, request: object, *, timeout: float) -> object:
        self.calls.append((request, timeout))
        if self.gate is not None:
            await self.gate.wait()
        if isinstance(self.response, BaseException):
            raise self.response
        return self.response


class _SynchronousFailure:
    def __init__(self) -> None:
        self.calls = 0

    def __call__(self, request: object, *, timeout: float) -> Any:
        self.calls += 1
        raise RuntimeError("secret local call-construction detail")


class _Stub:
    def __init__(self, write: object) -> None:
        self.WriteProperty = write


def _client(
    monkeypatch: pytest.MonkeyPatch,
    write: object,
) -> RuntimeHostClient:
    monkeypatch.setattr(
        client_module.services,
        "RuntimeHostRemoteApiStub",
        lambda channel: _Stub(write),
    )
    channel = RuntimeHostChannel(_UnderlyingChannel())  # type: ignore[arg-type]
    return RuntimeHostClient(channel)


def _target() -> PropertyTarget:
    return PropertyTarget(
        "kel-103",
        "attachment-7",
        "electronic-load-01",
        "target-current",
    )


def _success(value: float = 0.1) -> contract.PropertyOperationResult:
    response = contract.PropertyOperationResult(
        status=contract.PROPERTY_OPERATION_STATUS_SUCCESS
    )
    response.confirmed_value.value.numeric_value = value
    response.confirmed_value.timestamp_utc.FromJsonString(
        "2026-08-09T10:11:12Z"
    )
    response.confirmed_value.quality = contract.PROPERTY_QUALITY_GOOD
    return response


@pytest.mark.parametrize(
    ("value", "kind", "wire_value"),
    [
        (True, "boolean_value", True),
        ("CC", "string_value", "CC"),
        (0.1, "numeric_value", 0.1),
        (42, "numeric_value", 42.0),
        (b"\x00\xff", "byte_array_value", b"\x00\xff"),
    ],
)
@_async_test
async def test_write_property_encodes_exact_request_and_invokes_once(
    monkeypatch: pytest.MonkeyPatch,
    value: object,
    kind: str,
    wire_value: object,
) -> None:
    write = _WriteCall(_success())
    client = _client(monkeypatch, write)

    result = await client.write_property(_target(), value, timeout=2.5)  # type: ignore[arg-type]

    assert result.status is PropertyOperationStatus.SUCCESS
    assert result.confirmed_value is not None
    assert len(write.calls) == 1
    request, timeout = write.calls[0]
    assert isinstance(request, contract.WritePropertyRequest)
    assert request.target == contract.PropertyTarget(
        endpoint_id="kel-103",
        attachment_generation="attachment-7",
        instrument_id="electronic-load-01",
        property_id="target-current",
    )
    assert request.requested_value.WhichOneof("kind") == kind
    assert getattr(request.requested_value, kind) == wire_value
    assert timeout == 2.5


@_async_test
async def test_write_property_uses_bounded_default_timeout(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    write = _WriteCall(_success())
    client = _client(monkeypatch, write)

    await client.write_property(_target(), 0.1)

    assert write.calls[0][1] == 10.0


@pytest.mark.parametrize(
    "value",
    [0, -1, True, "10", math.inf, -math.inf, math.nan, 10**1000, None],
)
@_async_test
async def test_write_property_rejects_invalid_timeout_before_rpc(
    monkeypatch: pytest.MonkeyPatch,
    value: object,
) -> None:
    write = _WriteCall(_success())
    client = _client(monkeypatch, write)

    with pytest.raises(RuntimeHostMutationError) as captured:
        await client.write_property(_target(), 0.1, timeout=value)  # type: ignore[arg-type]

    assert captured.value.code == "mutation-rpc-timeout-invalid"
    assert captured.value.classification is MutationFailureClassification.NOT_SENT
    assert write.calls == []


@pytest.mark.parametrize(
    ("value", "code"),
    [
        (None, "mutation-value-absent"),
        (math.inf, "mutation-number-invalid"),
        (math.nan, "mutation-number-invalid"),
        (2**53 + 1, "mutation-number-not-exact"),
        (bytearray(b"secret"), "mutation-value-type-unsupported"),
        ([], "mutation-value-type-unsupported"),
    ],
)
@_async_test
async def test_write_property_rejects_invalid_value_before_rpc(
    monkeypatch: pytest.MonkeyPatch,
    value: object,
    code: str,
) -> None:
    write = _WriteCall(_success())
    client = _client(monkeypatch, write)

    with pytest.raises(RuntimeHostMutationError) as captured:
        await client.write_property(_target(), value)  # type: ignore[arg-type]

    assert captured.value.code == code
    assert captured.value.classification is MutationFailureClassification.NOT_SENT
    assert write.calls == []
    assert "secret" not in str(captured.value)


@_async_test
async def test_write_property_rejects_invalid_target_before_rpc(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    write = _WriteCall(_success())
    client = _client(monkeypatch, write)

    with pytest.raises(RuntimeHostMutationError) as captured:
        await client.write_property(object(), 0.1)  # type: ignore[arg-type]

    assert captured.value.code == "mutation-property-target-invalid"
    assert captured.value.classification is MutationFailureClassification.NOT_SENT
    assert write.calls == []


@_async_test
async def test_write_property_maps_returned_rejection_without_diagnostic(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    response = contract.PropertyOperationResult(
        status=contract.PROPERTY_OPERATION_STATUS_INVALID_VALUE
    )
    response.diagnostic = "secret endpoint detail"
    write = _WriteCall(response)
    client = _client(monkeypatch, write)

    with pytest.raises(RuntimeHostMutationError) as captured:
        await client.write_property(_target(), 0.1)

    assert captured.value.code == "mutation-property-invalid-value"
    assert captured.value.classification is MutationFailureClassification.REJECTED
    assert "secret" not in str(captured.value)
    assert len(write.calls) == 1


@pytest.mark.parametrize(
    ("response", "code"),
    [
        (
            contract.PropertyOperationResult(
                status=contract.PROPERTY_OPERATION_STATUS_ENDPOINT_FAILURE
            ),
            "mutation-property-endpoint-failure",
        ),
        (
            contract.PropertyOperationResult(
                status=contract.PROPERTY_OPERATION_STATUS_TIMED_OUT
            ),
            "mutation-property-timed-out",
        ),
        (
            contract.PropertyOperationResult(),
            "mutation-property-result-invalid",
        ),
    ],
)
@_async_test
async def test_write_property_maps_ambiguous_result_uncertain(
    monkeypatch: pytest.MonkeyPatch,
    response: contract.PropertyOperationResult,
    code: str,
) -> None:
    write = _WriteCall(response)
    client = _client(monkeypatch, write)

    with pytest.raises(RuntimeHostMutationError) as captured:
        await client.write_property(_target(), 0.1)

    assert captured.value.code == code
    assert (
        captured.value.classification
        is MutationFailureClassification.OUTCOME_UNCERTAIN
    )
    assert captured.value.outcome_uncertain
    assert not captured.value.automatic_retry_permitted
    assert len(write.calls) == 1


@pytest.mark.parametrize(
    ("status", "classification", "code"),
    [
        (
            grpc.StatusCode.PERMISSION_DENIED,
            MutationFailureClassification.REJECTED,
            "mutation-rpc-permission-denied",
        ),
        (
            grpc.StatusCode.UNAVAILABLE,
            MutationFailureClassification.OUTCOME_UNCERTAIN,
            "mutation-rpc-outcome-uncertain",
        ),
        (
            grpc.StatusCode.DEADLINE_EXCEEDED,
            MutationFailureClassification.OUTCOME_UNCERTAIN,
            "mutation-rpc-outcome-uncertain",
        ),
    ],
)
@_async_test
async def test_write_property_maps_rpc_failure_without_retry_or_details(
    monkeypatch: pytest.MonkeyPatch,
    status: grpc.StatusCode,
    classification: MutationFailureClassification,
    code: str,
) -> None:
    write = _WriteCall(_RpcFailure(status))
    client = _client(monkeypatch, write)

    with pytest.raises(RuntimeHostMutationError) as captured:
        await client.write_property(_target(), 0.1)

    assert captured.value.code == code
    assert captured.value.classification is classification
    assert not captured.value.automatic_retry_permitted
    assert "secret" not in str(captured.value)
    assert len(write.calls) == 1


@_async_test
async def test_write_property_classifies_synchronous_call_failure_not_sent(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    write = _SynchronousFailure()
    client = _client(monkeypatch, write)

    with pytest.raises(RuntimeHostMutationError) as captured:
        await client.write_property(_target(), 0.1)

    assert captured.value.code == "mutation-rpc-not-sent"
    assert captured.value.classification is MutationFailureClassification.NOT_SENT
    assert "secret" not in str(captured.value)
    assert write.calls == 1


@_async_test
async def test_write_property_converts_caller_cancellation_to_uncertain(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    write = _WriteCall(_success())
    write.gate = asyncio.Event()
    client = _client(monkeypatch, write)
    operation = asyncio.create_task(client.write_property(_target(), 0.1))
    while not write.calls:
        await asyncio.sleep(0)

    operation.cancel()

    with pytest.raises(RuntimeHostMutationError) as captured:
        await operation
    assert captured.value.code == "mutation-rpc-cancelled"
    assert (
        captured.value.classification
        is MutationFailureClassification.OUTCOME_UNCERTAIN
    )
    assert captured.value.outcome_uncertain
    assert not captured.value.automatic_retry_permitted
    assert len(write.calls) == 1
