"""Immutable authoritative Property models and strict transport projection."""

from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime, timezone
from enum import Enum
import math
from typing import TypeAlias

from hase._generated import runtime_host_remote_api_v1_pb2 as contract


class PropertyProjectionError(ValueError):
    """A sanitized invalid Property model or transport result failure."""

    def __init__(self, code: str) -> None:
        self.code = code
        super().__init__(f"Runtime Host Property projection failed: {code}.")


class PropertyQuality(Enum):
    GOOD = "good"
    UNCERTAIN = "uncertain"
    BAD = "bad"


class PropertyOperationStatus(Enum):
    SUCCESS = "success"
    ATTACHMENT_NOT_CURRENT = "attachment-not-current"
    INSTRUMENT_NOT_FOUND = "instrument-not-found"
    PROPERTY_NOT_FOUND = "property-not-found"
    READ_NOT_SUPPORTED = "read-not-supported"
    WRITE_NOT_SUPPORTED = "write-not-supported"
    INVALID_VALUE = "invalid-value"
    ENDPOINT_UNAVAILABLE = "endpoint-unavailable"
    ENDPOINT_REJECTED = "endpoint-rejected"
    ENDPOINT_FAILURE = "endpoint-failure"
    TIMED_OUT = "timed-out"


PropertyScalar: TypeAlias = bool | str | float | bytes | None


def _required_text(value: str) -> str:
    if not isinstance(value, str) or not value or value != value.strip():
        raise PropertyProjectionError("property-text-invalid")
    return value


@dataclass(frozen=True, slots=True)
class PropertyTarget:
    endpoint_id: str
    attachment_generation: str
    instrument_id: str
    property_id: str

    def __post_init__(self) -> None:
        _required_text(self.endpoint_id)
        _required_text(self.attachment_generation)
        _required_text(self.instrument_id)
        _required_text(self.property_id)


@dataclass(frozen=True, slots=True)
class PropertyValue:
    value: PropertyScalar
    timestamp_utc: datetime
    quality: PropertyQuality


@dataclass(frozen=True, slots=True)
class PropertyOperationResult:
    status: PropertyOperationStatus
    confirmed_value: PropertyValue | None
    diagnostic: str | None

    @property
    def is_success(self) -> bool:
        return self.status is PropertyOperationStatus.SUCCESS


_QUALITIES = {
    contract.PROPERTY_QUALITY_GOOD: PropertyQuality.GOOD,
    contract.PROPERTY_QUALITY_UNCERTAIN: PropertyQuality.UNCERTAIN,
    contract.PROPERTY_QUALITY_BAD: PropertyQuality.BAD,
}

_STATUSES = {
    contract.PROPERTY_OPERATION_STATUS_SUCCESS: PropertyOperationStatus.SUCCESS,
    contract.PROPERTY_OPERATION_STATUS_ATTACHMENT_NOT_CURRENT:
        PropertyOperationStatus.ATTACHMENT_NOT_CURRENT,
    contract.PROPERTY_OPERATION_STATUS_INSTRUMENT_NOT_FOUND:
        PropertyOperationStatus.INSTRUMENT_NOT_FOUND,
    contract.PROPERTY_OPERATION_STATUS_PROPERTY_NOT_FOUND:
        PropertyOperationStatus.PROPERTY_NOT_FOUND,
    contract.PROPERTY_OPERATION_STATUS_READ_NOT_SUPPORTED:
        PropertyOperationStatus.READ_NOT_SUPPORTED,
    contract.PROPERTY_OPERATION_STATUS_WRITE_NOT_SUPPORTED:
        PropertyOperationStatus.WRITE_NOT_SUPPORTED,
    contract.PROPERTY_OPERATION_STATUS_INVALID_VALUE:
        PropertyOperationStatus.INVALID_VALUE,
    contract.PROPERTY_OPERATION_STATUS_ENDPOINT_UNAVAILABLE:
        PropertyOperationStatus.ENDPOINT_UNAVAILABLE,
    contract.PROPERTY_OPERATION_STATUS_ENDPOINT_REJECTED:
        PropertyOperationStatus.ENDPOINT_REJECTED,
    contract.PROPERTY_OPERATION_STATUS_ENDPOINT_FAILURE:
        PropertyOperationStatus.ENDPOINT_FAILURE,
    contract.PROPERTY_OPERATION_STATUS_TIMED_OUT:
        PropertyOperationStatus.TIMED_OUT,
}


def _remote_value(source: contract.RemoteValue) -> PropertyScalar:
    kind = source.WhichOneof("kind")
    if kind is None:
        return None
    if kind == "boolean_value":
        return source.boolean_value
    if kind == "string_value":
        return source.string_value
    if kind == "byte_array_value":
        return bytes(source.byte_array_value)
    if kind == "numeric_value":
        value = float(source.numeric_value)
        if not math.isfinite(value):
            raise PropertyProjectionError("property-number-invalid")
        return value
    raise PropertyProjectionError("property-value-kind-invalid")


def _property_value(source: contract.PropertyValue) -> PropertyValue:
    if not source.HasField("timestamp_utc"):
        raise PropertyProjectionError("property-timestamp-missing")
    try:
        timestamp = source.timestamp_utc.ToDatetime(tzinfo=timezone.utc)
    except (OverflowError, ValueError):
        raise PropertyProjectionError("property-timestamp-invalid") from None
    try:
        quality = _QUALITIES[source.quality]
    except KeyError:
        raise PropertyProjectionError("property-quality-invalid") from None
    value = _remote_value(source.value) if source.HasField("value") else None
    return PropertyValue(value, timestamp, quality)


def project_property_target(source: contract.PropertyTarget) -> PropertyTarget:
    """Project one generated Property target into its immutable public model."""

    if not isinstance(source, contract.PropertyTarget):
        raise PropertyProjectionError("property-target-type-invalid")
    return PropertyTarget(
        source.endpoint_id,
        source.attachment_generation,
        source.instrument_id,
        source.property_id,
    )


def project_property_operation_result(
    source: contract.PropertyOperationResult,
) -> PropertyOperationResult:
    """Project one authoritative operation result with strict shape checks."""

    if not isinstance(source, contract.PropertyOperationResult):
        raise PropertyProjectionError("property-result-type-invalid")
    try:
        status = _STATUSES[source.status]
    except KeyError:
        raise PropertyProjectionError("property-status-invalid") from None

    has_confirmed_value = source.HasField("confirmed_value")
    diagnostic = source.diagnostic if source.HasField("diagnostic") else None
    if status is PropertyOperationStatus.SUCCESS:
        if not has_confirmed_value or diagnostic is not None:
            raise PropertyProjectionError("property-success-shape-invalid")
    elif has_confirmed_value:
        raise PropertyProjectionError("property-failure-shape-invalid")

    confirmed_value = (
        _property_value(source.confirmed_value) if has_confirmed_value else None
    )
    return PropertyOperationResult(status, confirmed_value, diagnostic)


__all__ = [
    "PropertyOperationResult",
    "PropertyOperationStatus",
    "PropertyProjectionError",
    "PropertyQuality",
    "PropertyScalar",
    "PropertyTarget",
    "PropertyValue",
    "project_property_operation_result",
    "project_property_target",
]
