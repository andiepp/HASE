import asyncio
from functools import wraps
import math
from typing import Any, Callable, Coroutine, TypeVar

import grpc
import pytest

import hase.mutation as mutation_module
from hase import MutationFailureClassification
from hase import RuntimeHostMutationError
from hase import normalize_mutation_value


_T = TypeVar("_T")


def _async_test(
    function: Callable[..., Coroutine[Any, Any, _T]],
) -> Callable[..., _T]:
    @wraps(function)
    def run(*args: object, **kwargs: object) -> _T:
        return asyncio.run(function(*args, **kwargs))

    return run


class _RpcFailure(grpc.RpcError):
    def __init__(self, status: grpc.StatusCode) -> None:
        super().__init__()
        self._status = status

    def code(self) -> grpc.StatusCode:
        return self._status

    def details(self) -> str:
        return "secret server, credential, and endpoint details"


class _MutationCall:
    def __init__(self, result: object) -> None:
        self.result = result
        self.calls: list[tuple[object, float]] = []

    async def __call__(self, request: object, *, timeout: float) -> object:
        self.calls.append((request, timeout))
        if isinstance(self.result, BaseException):
            raise self.result
        return self.result


class _SynchronousFailure:
    def __init__(self, failure: BaseException) -> None:
        self.failure = failure
        self.calls = 0

    def __call__(self, request: object, *, timeout: float) -> Any:
        self.calls += 1
        raise self.failure


@pytest.mark.parametrize(
    ("value", "expected", "expected_type"),
    [
        (False, False, bool),
        (True, True, bool),
        ("", "", str),
        ("CC", "CC", str),
        (b"", b"", bytes),
        (b"\x00\xff\x0d\x0a", b"\x00\xff\x0d\x0a", bytes),
        (0, 0.0, float),
        (42, 42.0, float),
        (-(2**53), float(-(2**53)), float),
        (2**53, float(2**53), float),
        (-0.0, -0.0, float),
        (1.25, 1.25, float),
    ],
)
def test_normalize_mutation_value_preserves_closed_supported_set(
    value: object,
    expected: object,
    expected_type: type,
) -> None:
    result = normalize_mutation_value(value)

    assert type(result) is expected_type
    assert result == expected
    if value == 0.0 and type(value) is float:
        assert math.copysign(1.0, result) == math.copysign(1.0, value)


@pytest.mark.parametrize(
    ("value", "code"),
    [
        (None, "mutation-value-absent"),
        (math.inf, "mutation-number-invalid"),
        (-math.inf, "mutation-number-invalid"),
        (math.nan, "mutation-number-invalid"),
        (2**53 + 1, "mutation-number-not-exact"),
        (10**1000, "mutation-number-invalid"),
        (bytearray(b"secret"), "mutation-value-type-unsupported"),
        (memoryview(b"secret"), "mutation-value-type-unsupported"),
        ([], "mutation-value-type-unsupported"),
        ({}, "mutation-value-type-unsupported"),
    ],
)
def test_normalize_mutation_value_rejects_before_transport(
    value: object,
    code: str,
) -> None:
    with pytest.raises(RuntimeHostMutationError) as failure:
        normalize_mutation_value(value)

    assert failure.value.code == code
    assert failure.value.classification is MutationFailureClassification.NOT_SENT
    assert not failure.value.outcome_uncertain
    assert not failure.value.automatic_retry_permitted
    assert "secret" not in str(failure.value)


@pytest.mark.parametrize(
    ("value", "kind", "wire_value"),
    [
        (True, "boolean_value", True),
        ("", "string_value", ""),
        (1.25, "numeric_value", 1.25),
        (42, "numeric_value", 42.0),
        (b"\x00\xff", "byte_array_value", b"\x00\xff"),
    ],
)
def test_internal_encoder_sets_exactly_one_wire_variant(
    value: object,
    kind: str,
    wire_value: object,
) -> None:
    encoded = mutation_module._encode_mutation_value(value)

    assert encoded.WhichOneof("kind") == kind
    assert getattr(encoded, kind) == wire_value


@pytest.mark.parametrize(
    "classification",
    list(MutationFailureClassification),
)
def test_mutation_error_exposes_only_stable_metadata_and_never_allows_retry(
    classification: MutationFailureClassification,
) -> None:
    failure = RuntimeHostMutationError("stable-code", classification)

    assert failure.code == "stable-code"
    assert failure.classification is classification
    assert failure.outcome_uncertain == (
        classification is MutationFailureClassification.OUTCOME_UNCERTAIN
    )
    assert not failure.automatic_retry_permitted
    assert str(failure) == "Runtime Host mutation failed: stable-code."


@pytest.mark.parametrize(
    ("code", "classification"),
    [
        ("", MutationFailureClassification.NOT_SENT),
        (" whitespace ", MutationFailureClassification.REJECTED),
        ("code", "outcome-uncertain"),
        (None, MutationFailureClassification.NOT_SENT),
    ],
)
def test_mutation_error_rejects_invalid_metadata(
    code: object,
    classification: object,
) -> None:
    with pytest.raises(ValueError) as failure:
        RuntimeHostMutationError(code, classification)  # type: ignore[arg-type]
    assert str(failure.value) == "Invalid Runtime Host mutation error metadata."


@_async_test
async def test_internal_mutation_invoker_calls_exactly_once_and_returns_response() -> None:
    response = object()
    request = object()
    operation = _MutationCall(response)

    result = await mutation_module._invoke_mutation_once(operation, request, 2.5)

    assert result is response
    assert operation.calls == [(request, 2.5)]


@pytest.mark.parametrize(
    "failure",
    [
        RuntimeError("secret local detail"),
        _RpcFailure(grpc.StatusCode.UNAVAILABLE),
        _RpcFailure(grpc.StatusCode.PERMISSION_DENIED),
    ],
)
@_async_test
async def test_internal_mutation_invoker_classifies_synchronous_failure_not_sent(
    failure: BaseException,
) -> None:
    operation = _SynchronousFailure(failure)

    with pytest.raises(RuntimeHostMutationError) as captured:
        await mutation_module._invoke_mutation_once(operation, object(), 2.5)

    assert captured.value.classification is MutationFailureClassification.NOT_SENT
    assert not captured.value.outcome_uncertain
    assert not captured.value.automatic_retry_permitted
    assert "secret" not in str(captured.value)
    assert operation.calls == 1


@pytest.mark.parametrize(
    ("status", "code"),
    [
        (grpc.StatusCode.INVALID_ARGUMENT, "mutation-rpc-invalid-argument"),
        (grpc.StatusCode.NOT_FOUND, "mutation-rpc-not-found"),
        (grpc.StatusCode.ALREADY_EXISTS, "mutation-rpc-already-exists"),
        (grpc.StatusCode.PERMISSION_DENIED, "mutation-rpc-permission-denied"),
        (grpc.StatusCode.FAILED_PRECONDITION, "mutation-rpc-failed-precondition"),
        (grpc.StatusCode.OUT_OF_RANGE, "mutation-rpc-out-of-range"),
        (grpc.StatusCode.UNAUTHENTICATED, "mutation-rpc-unauthenticated"),
        (grpc.StatusCode.UNIMPLEMENTED, "mutation-rpc-unimplemented"),
    ],
)
@_async_test
async def test_internal_mutation_invoker_classifies_server_rejection(
    status: grpc.StatusCode,
    code: str,
) -> None:
    operation = _MutationCall(_RpcFailure(status))

    with pytest.raises(RuntimeHostMutationError) as captured:
        await mutation_module._invoke_mutation_once(operation, object(), 2.5)

    assert captured.value.code == code
    assert captured.value.classification is MutationFailureClassification.REJECTED
    assert not captured.value.outcome_uncertain
    assert not captured.value.automatic_retry_permitted
    assert "secret" not in str(captured.value)
    assert len(operation.calls) == 1


@pytest.mark.parametrize(
    "failure",
    [
        _RpcFailure(grpc.StatusCode.DEADLINE_EXCEEDED),
        _RpcFailure(grpc.StatusCode.UNAVAILABLE),
        _RpcFailure(grpc.StatusCode.CANCELLED),
        _RpcFailure(grpc.StatusCode.INTERNAL),
        _RpcFailure(grpc.StatusCode.UNKNOWN),
        RuntimeError("secret transport detail"),
    ],
)
@_async_test
async def test_internal_mutation_invoker_classifies_post_invocation_failure_uncertain(
    failure: BaseException,
) -> None:
    operation = _MutationCall(failure)

    with pytest.raises(RuntimeHostMutationError) as captured:
        await mutation_module._invoke_mutation_once(operation, object(), 2.5)

    assert captured.value.code == "mutation-rpc-outcome-uncertain"
    assert (
        captured.value.classification
        is MutationFailureClassification.OUTCOME_UNCERTAIN
    )
    assert captured.value.outcome_uncertain
    assert not captured.value.automatic_retry_permitted
    assert "secret" not in str(captured.value)
    assert len(operation.calls) == 1


@_async_test
async def test_internal_mutation_invoker_converts_caller_cancellation_to_uncertain() -> None:
    async def cancelled(request: object, *, timeout: float) -> object:
        raise asyncio.CancelledError

    with pytest.raises(RuntimeHostMutationError) as captured:
        await mutation_module._invoke_mutation_once(cancelled, object(), 2.5)

    assert captured.value.code == "mutation-rpc-cancelled"
    assert (
        captured.value.classification
        is MutationFailureClassification.OUTCOME_UNCERTAIN
    )
    assert captured.value.outcome_uncertain
    assert not captured.value.automatic_retry_permitted
