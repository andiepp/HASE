"""Immutable command targets and strict command-result projection."""

from __future__ import annotations

from dataclasses import dataclass
from enum import Enum
import math
from typing import TypeAlias

from hase._generated import runtime_host_remote_api_v1_pb2 as contract


class CommandProjectionError(ValueError):
    def __init__(self, code: str) -> None:
        self.code = code
        super().__init__(f"Runtime Host command projection failed: {code}.")


class CommandOperationStatus(Enum):
    SUCCESS = "success"
    ATTACHMENT_NOT_CURRENT = "attachment-not-current"
    INSTRUMENT_NOT_FOUND = "instrument-not-found"
    COMMAND_NOT_FOUND = "command-not-found"
    ARGUMENT_NOT_SUPPORTED = "argument-not-supported"
    ENDPOINT_UNAVAILABLE = "endpoint-unavailable"
    ENDPOINT_REJECTED = "endpoint-rejected"
    ENDPOINT_FAILURE = "endpoint-failure"
    TIMED_OUT = "timed-out"


CommandScalar: TypeAlias = bool | str | float | bytes | None


def _text(value: str) -> str:
    if not isinstance(value, str) or not value or value != value.strip():
        raise CommandProjectionError("command-text-invalid")
    return value


@dataclass(frozen=True, slots=True)
class CommandTarget:
    endpoint_id: str
    attachment_generation: str
    instrument_id: str
    command_path_segments: tuple[str, ...]

    def __post_init__(self) -> None:
        _text(self.endpoint_id)
        _text(self.attachment_generation)
        _text(self.instrument_id)
        if (not isinstance(self.command_path_segments, tuple)
                or not self.command_path_segments):
            raise CommandProjectionError("command-path-invalid")
        for segment in self.command_path_segments:
            _text(segment)


@dataclass(frozen=True, slots=True)
class CommandOperationResult:
    status: CommandOperationStatus
    return_value: CommandScalar
    diagnostic: str | None

    @property
    def is_success(self) -> bool:
        return self.status is CommandOperationStatus.SUCCESS


_STATUSES = {
    contract.COMMAND_OPERATION_STATUS_SUCCESS: CommandOperationStatus.SUCCESS,
    contract.COMMAND_OPERATION_STATUS_ATTACHMENT_NOT_CURRENT:
        CommandOperationStatus.ATTACHMENT_NOT_CURRENT,
    contract.COMMAND_OPERATION_STATUS_INSTRUMENT_NOT_FOUND:
        CommandOperationStatus.INSTRUMENT_NOT_FOUND,
    contract.COMMAND_OPERATION_STATUS_COMMAND_NOT_FOUND:
        CommandOperationStatus.COMMAND_NOT_FOUND,
    contract.COMMAND_OPERATION_STATUS_ARGUMENT_NOT_SUPPORTED:
        CommandOperationStatus.ARGUMENT_NOT_SUPPORTED,
    contract.COMMAND_OPERATION_STATUS_ENDPOINT_UNAVAILABLE:
        CommandOperationStatus.ENDPOINT_UNAVAILABLE,
    contract.COMMAND_OPERATION_STATUS_ENDPOINT_REJECTED:
        CommandOperationStatus.ENDPOINT_REJECTED,
    contract.COMMAND_OPERATION_STATUS_ENDPOINT_FAILURE:
        CommandOperationStatus.ENDPOINT_FAILURE,
    contract.COMMAND_OPERATION_STATUS_TIMED_OUT: CommandOperationStatus.TIMED_OUT,
}


def _value(source: contract.RemoteValue) -> CommandScalar:
    kind = source.WhichOneof("kind")
    if kind is None:
        return None
    value = getattr(source, kind)
    if kind == "byte_array_value":
        return bytes(value)
    if kind == "numeric_value":
        result = float(value)
        if not math.isfinite(result):
            raise CommandProjectionError("command-number-invalid")
        return result
    if kind in ("boolean_value", "string_value"):
        return value
    raise CommandProjectionError("command-value-kind-invalid")


def project_command_operation_result(
    source: contract.CommandOperationResult,
) -> CommandOperationResult:
    if not isinstance(source, contract.CommandOperationResult):
        raise CommandProjectionError("command-result-type-invalid")
    try:
        status = _STATUSES[source.status]
    except KeyError:
        raise CommandProjectionError("command-status-invalid") from None
    has_value = source.HasField("return_value")
    diagnostic = source.diagnostic if source.HasField("diagnostic") else None
    if status is CommandOperationStatus.SUCCESS:
        if diagnostic is not None:
            raise CommandProjectionError("command-success-shape-invalid")
    elif has_value:
        raise CommandProjectionError("command-failure-shape-invalid")
    return CommandOperationResult(
        status, _value(source.return_value) if has_value else None, diagnostic)


__all__ = ["CommandOperationResult", "CommandOperationStatus",
    "CommandProjectionError", "CommandScalar", "CommandTarget",
    "project_command_operation_result"]
