"""Strict mutation values and explicit uncertain-outcome semantics."""

from __future__ import annotations

from enum import Enum
import math
from typing import TypeAlias

from hase._generated import runtime_host_remote_api_v1_pb2 as contract


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


__all__ = [
    "MutationFailureClassification",
    "MutationValue",
    "RuntimeHostMutationError",
    "normalize_mutation_value",
]
