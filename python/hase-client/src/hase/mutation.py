"""Strict mutation values and explicit uncertain-outcome semantics."""

from __future__ import annotations

import asyncio
from enum import Enum
import math
from typing import Any, Awaitable, Callable, TypeAlias

import grpc

from hase._generated import runtime_host_remote_api_v1_pb2 as contract
from hase.property import PropertyOperationResult
from hase.property import PropertyOperationStatus
from hase.property import PropertyProjectionError
from hase.property import project_property_operation_result


class MutationFailureClassification(Enum):
    """Whether a failed mutation could have reached the Runtime Host."""

    NOT_SENT = "not-sent"
    REJECTED = "rejected"
    OUTCOME_UNCERTAIN = "outcome-uncertain"


class RuntimeHostMutationError(RuntimeError):
    """A sanitized mutation failure that is never automatically retryable."""

    def __init__(
        self,
        code: str,
        classification: MutationFailureClassification,
    ) -> None:
        if (
            not isinstance(code, str)
            or not code
            or code != code.strip()
            or not isinstance(classification, MutationFailureClassification)
        ):
            raise ValueError("Invalid Runtime Host mutation error metadata.")
        self.code = code
        self.classification = classification
        super().__init__(f"Runtime Host mutation failed: {code}.")

    @property
    def outcome_uncertain(self) -> bool:
        return self.classification is MutationFailureClassification.OUTCOME_UNCERTAIN

    @property
    def automatic_retry_permitted(self) -> bool:
        return False


MutationValue: TypeAlias = bool | str | int | float | bytes


def _not_sent(code: str) -> RuntimeHostMutationError:
    return RuntimeHostMutationError(code, MutationFailureClassification.NOT_SENT)


def normalize_mutation_value(value: object) -> MutationValue:
    """Normalize one supported value without constructing a transport object."""

    if value is None:
        raise _not_sent("mutation-value-absent")
    if type(value) is bool:
        return value
    if type(value) is str:
        return value
    if type(value) is bytes:
        return value
    if type(value) is int:
        try:
            numeric = float(value)
        except OverflowError:
            raise _not_sent("mutation-number-invalid") from None
        if not math.isfinite(numeric):
            raise _not_sent("mutation-number-invalid")
        if int(numeric) != value:
            raise _not_sent("mutation-number-not-exact")
        return numeric
    if type(value) is float:
        if not math.isfinite(value):
            raise _not_sent("mutation-number-invalid")
        return value
    raise _not_sent("mutation-value-type-unsupported")


def _encode_mutation_value(value: object) -> contract.RemoteValue:
    normalized = normalize_mutation_value(value)
    result = contract.RemoteValue()
    if type(normalized) is bool:
        result.boolean_value = normalized
    elif type(normalized) is str:
        result.string_value = normalized
    elif type(normalized) is bytes:
        result.byte_array_value = normalized
    else:
        result.numeric_value = normalized
    return result


_REJECTED_RPC_CODES = {
    grpc.StatusCode.INVALID_ARGUMENT: "mutation-rpc-invalid-argument",
    grpc.StatusCode.NOT_FOUND: "mutation-rpc-not-found",
    grpc.StatusCode.ALREADY_EXISTS: "mutation-rpc-already-exists",
    grpc.StatusCode.PERMISSION_DENIED: "mutation-rpc-permission-denied",
    grpc.StatusCode.FAILED_PRECONDITION: "mutation-rpc-failed-precondition",
    grpc.StatusCode.OUT_OF_RANGE: "mutation-rpc-out-of-range",
    grpc.StatusCode.UNAUTHENTICATED: "mutation-rpc-unauthenticated",
    grpc.StatusCode.UNIMPLEMENTED: "mutation-rpc-unimplemented",
}


def _rpc_status(failure: grpc.RpcError) -> grpc.StatusCode | None:
    try:
        status = failure.code()
    except Exception:
        return None
    return status if isinstance(status, grpc.StatusCode) else None


async def _invoke_mutation_once(
    operation: Callable[..., Awaitable[Any]],
    request: Any,
    timeout: float,
) -> Any:
    """Invoke one prepared mutation without retry, replay, or reconnection."""

    try:
        pending = operation(request, timeout=timeout)
    except grpc.RpcError as failure:
        status = _rpc_status(failure)
        code = _REJECTED_RPC_CODES.get(status, "mutation-rpc-not-sent")
        raise RuntimeHostMutationError(
            code,
            MutationFailureClassification.NOT_SENT,
        ) from None
    except Exception:
        raise RuntimeHostMutationError(
            "mutation-rpc-not-sent",
            MutationFailureClassification.NOT_SENT,
        ) from None

    try:
        return await pending
    except asyncio.CancelledError:
        raise RuntimeHostMutationError(
            "mutation-rpc-cancelled",
            MutationFailureClassification.OUTCOME_UNCERTAIN,
        ) from None
    except grpc.RpcError as failure:
        status = _rpc_status(failure)
        rejection_code = _REJECTED_RPC_CODES.get(status)
        if rejection_code is not None:
            raise RuntimeHostMutationError(
                rejection_code,
                MutationFailureClassification.REJECTED,
            ) from None
        raise RuntimeHostMutationError(
            "mutation-rpc-outcome-uncertain",
            MutationFailureClassification.OUTCOME_UNCERTAIN,
        ) from None
    except Exception:
        raise RuntimeHostMutationError(
            "mutation-rpc-outcome-uncertain",
            MutationFailureClassification.OUTCOME_UNCERTAIN,
        ) from None


_REJECTED_PROPERTY_STATUSES = {
    PropertyOperationStatus.ATTACHMENT_NOT_CURRENT:
        "mutation-property-attachment-not-current",
    PropertyOperationStatus.INSTRUMENT_NOT_FOUND:
        "mutation-property-instrument-not-found",
    PropertyOperationStatus.PROPERTY_NOT_FOUND:
        "mutation-property-not-found",
    PropertyOperationStatus.WRITE_NOT_SUPPORTED:
        "mutation-property-write-not-supported",
    PropertyOperationStatus.INVALID_VALUE:
        "mutation-property-invalid-value",
    PropertyOperationStatus.ENDPOINT_UNAVAILABLE:
        "mutation-property-endpoint-unavailable",
    PropertyOperationStatus.ENDPOINT_REJECTED:
        "mutation-property-endpoint-rejected",
}


def _project_property_mutation_result(
    source: contract.PropertyOperationResult,
) -> PropertyOperationResult:
    """Project one returned write result without exposing failure diagnostics."""

    try:
        result = project_property_operation_result(source)
    except (PropertyProjectionError, TypeError, ValueError):
        raise RuntimeHostMutationError(
            "mutation-property-result-invalid",
            MutationFailureClassification.OUTCOME_UNCERTAIN,
        ) from None

    if result.status is PropertyOperationStatus.SUCCESS:
        return result

    rejection_code = _REJECTED_PROPERTY_STATUSES.get(result.status)
    if rejection_code is not None:
        raise RuntimeHostMutationError(
            rejection_code,
            MutationFailureClassification.REJECTED,
        )

    if result.status is PropertyOperationStatus.ENDPOINT_FAILURE:
        code = "mutation-property-endpoint-failure"
    elif result.status is PropertyOperationStatus.TIMED_OUT:
        code = "mutation-property-timed-out"
    else:
        code = "mutation-property-result-invalid"
    raise RuntimeHostMutationError(
        code,
        MutationFailureClassification.OUTCOME_UNCERTAIN,
    )


__all__ = [
    "MutationFailureClassification",
    "MutationValue",
    "RuntimeHostMutationError",
    "normalize_mutation_value",
]
