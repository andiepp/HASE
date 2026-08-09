"""Immutable projections for the authorized Runtime Host diagnostic stream."""
from __future__ import annotations
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from enum import Enum
from hase._generated import runtime_host_remote_api_v1_pb2 as contract

class DiagnosticProjectionError(ValueError):
    def __init__(self, code: str):
        self.code = code
        super().__init__(f"Runtime Host diagnostic projection failed: {code}.")

class DiagnosticLevel(Enum):
    OPERATIONAL = "operational"
    PROTOCOL = "protocol"
    BYTES = "bytes"

class DiagnosticCategory(Enum):
    RUNTIME_ATTACHMENT = "runtime-attachment"
    RUNTIME_CONNECTION = "runtime-connection"
    RUNTIME_SYNCHRONIZATION = "runtime-synchronization"
    RUNTIME_RECOVERY = "runtime-recovery"
    RUNTIME_PROPERTY = "runtime-property"
    RUNTIME_COMMAND = "runtime-command"
    RUNTIME_EVENT = "runtime-event"
    PROTOCOL_EXCHANGE = "protocol-exchange"
    TRANSPORT_BYTES = "transport-bytes"

class DiagnosticSeverity(Enum):
    TRACE = "trace"
    INFORMATION = "information"
    WARNING = "warning"
    ERROR = "error"

class DiagnosticDirection(Enum):
    OUTBOUND = "outbound"
    INBOUND = "inbound"

class DiagnosticOutcome(Enum):
    SUCCEEDED = "succeeded"
    FAILED = "failed"
    CANCELLED = "cancelled"
    TIMED_OUT = "timed-out"

@dataclass(frozen=True, slots=True)
class DiagnosticByteSnapshot:
    original_byte_count: int
    captured_bytes: bytes
    is_truncated: bool

@dataclass(frozen=True, slots=True)
class DiagnosticRecord:
    runtime_host_id: str
    source_sequence: int
    timestamp_utc: datetime
    level: DiagnosticLevel
    category: DiagnosticCategory
    event_name: str
    severity: DiagnosticSeverity
    endpoint_id: str | None
    attachment_generation: str | None
    direction: DiagnosticDirection | None
    operation_id: str | None
    duration: timedelta | None
    outcome: DiagnosticOutcome | None
    details: tuple[tuple[str, str], ...]
    byte_snapshot: DiagnosticByteSnapshot | None

@dataclass(frozen=True, slots=True)
class DiagnosticObservation:
    sequence: int
    record: DiagnosticRecord

def _text(value: str, code: str) -> str:
    if not isinstance(value, str) or not value or value != value.strip():
        raise DiagnosticProjectionError(code)
    return value

_LEVELS = {contract.RUNTIME_DIAGNOSTIC_LEVEL_OPERATIONAL: DiagnosticLevel.OPERATIONAL,
    contract.RUNTIME_DIAGNOSTIC_LEVEL_PROTOCOL: DiagnosticLevel.PROTOCOL,
    contract.RUNTIME_DIAGNOSTIC_LEVEL_BYTES: DiagnosticLevel.BYTES}
_CATEGORIES = {
    contract.RUNTIME_DIAGNOSTIC_CATEGORY_RUNTIME_ATTACHMENT: DiagnosticCategory.RUNTIME_ATTACHMENT,
    contract.RUNTIME_DIAGNOSTIC_CATEGORY_RUNTIME_CONNECTION: DiagnosticCategory.RUNTIME_CONNECTION,
    contract.RUNTIME_DIAGNOSTIC_CATEGORY_RUNTIME_SYNCHRONIZATION: DiagnosticCategory.RUNTIME_SYNCHRONIZATION,
    contract.RUNTIME_DIAGNOSTIC_CATEGORY_RUNTIME_RECOVERY: DiagnosticCategory.RUNTIME_RECOVERY,
    contract.RUNTIME_DIAGNOSTIC_CATEGORY_RUNTIME_PROPERTY: DiagnosticCategory.RUNTIME_PROPERTY,
    contract.RUNTIME_DIAGNOSTIC_CATEGORY_RUNTIME_COMMAND: DiagnosticCategory.RUNTIME_COMMAND,
    contract.RUNTIME_DIAGNOSTIC_CATEGORY_RUNTIME_EVENT: DiagnosticCategory.RUNTIME_EVENT,
    contract.RUNTIME_DIAGNOSTIC_CATEGORY_PROTOCOL_EXCHANGE: DiagnosticCategory.PROTOCOL_EXCHANGE,
    contract.RUNTIME_DIAGNOSTIC_CATEGORY_TRANSPORT_BYTES: DiagnosticCategory.TRANSPORT_BYTES}
_SEVERITIES = {contract.RUNTIME_DIAGNOSTIC_SEVERITY_TRACE: DiagnosticSeverity.TRACE,
    contract.RUNTIME_DIAGNOSTIC_SEVERITY_INFORMATION: DiagnosticSeverity.INFORMATION,
    contract.RUNTIME_DIAGNOSTIC_SEVERITY_WARNING: DiagnosticSeverity.WARNING,
    contract.RUNTIME_DIAGNOSTIC_SEVERITY_ERROR: DiagnosticSeverity.ERROR}
_DIRECTIONS = {contract.RUNTIME_DIAGNOSTIC_DIRECTION_OUTBOUND: DiagnosticDirection.OUTBOUND,
    contract.RUNTIME_DIAGNOSTIC_DIRECTION_INBOUND: DiagnosticDirection.INBOUND}
_OUTCOMES = {contract.RUNTIME_DIAGNOSTIC_OUTCOME_SUCCEEDED: DiagnosticOutcome.SUCCEEDED,
    contract.RUNTIME_DIAGNOSTIC_OUTCOME_FAILED: DiagnosticOutcome.FAILED,
    contract.RUNTIME_DIAGNOSTIC_OUTCOME_CANCELLED: DiagnosticOutcome.CANCELLED,
    contract.RUNTIME_DIAGNOSTIC_OUTCOME_TIMED_OUT: DiagnosticOutcome.TIMED_OUT}

def project_diagnostic_observation(source: contract.ProjectedDiagnosticObservation) -> DiagnosticObservation:
    if not isinstance(source, contract.ProjectedDiagnosticObservation):
        raise DiagnosticProjectionError("diagnostic-observation-type-invalid")
    if source.sequence <= 0 or not source.HasField("record"):
        raise DiagnosticProjectionError("diagnostic-observation-shape-invalid")
    item = source.record
    try:
        if item.source_sequence <= 0 or not item.HasField("timestamp_utc"):
            raise DiagnosticProjectionError("diagnostic-record-shape-invalid")
        timestamp = item.timestamp_utc.ToDatetime(tzinfo=timezone.utc)
        level = _LEVELS[item.level]; category = _CATEGORIES[item.category]
        severity = _SEVERITIES[item.severity]
        endpoint = _text(item.endpoint_id, "diagnostic-endpoint-invalid") if item.HasField("endpoint_id") else None
        generation = _text(item.attachment_generation, "diagnostic-generation-invalid") if item.HasField("attachment_generation") else None
        if endpoint is None and generation is not None:
            raise DiagnosticProjectionError("diagnostic-scope-incomplete")
        direction = _DIRECTIONS[item.direction] if item.HasField("direction") else None
        operation = _text(item.operation_id, "diagnostic-operation-invalid") if item.HasField("operation_id") else None
        duration = item.duration.ToTimedelta() if item.HasField("duration") else None
        outcome = _OUTCOMES[item.outcome] if item.HasField("outcome") else None
        details = tuple(sorted((_text(k, "diagnostic-detail-invalid"), str(v)) for k, v in item.details.items()))
        byte_snapshot = None
        if item.HasField("byte_snapshot"):
            byte = item.byte_snapshot; captured = bytes(byte.captured_bytes)
            if byte.original_byte_count < len(captured) or byte.is_truncated != (byte.original_byte_count > len(captured)):
                raise DiagnosticProjectionError("diagnostic-bytes-invalid")
            byte_snapshot = DiagnosticByteSnapshot(int(byte.original_byte_count), captured, bool(byte.is_truncated))
        if level is DiagnosticLevel.BYTES and byte_snapshot is None:
            raise DiagnosticProjectionError("diagnostic-bytes-missing")
        return DiagnosticObservation(int(source.sequence), DiagnosticRecord(
            _text(item.runtime_host_id, "diagnostic-host-invalid"), int(item.source_sequence),
            timestamp, level, category, _text(item.event_name, "diagnostic-event-invalid"),
            severity, endpoint, generation, direction, operation, duration, outcome,
            details, byte_snapshot))
    except DiagnosticProjectionError: raise
    except (KeyError, OverflowError, TypeError, ValueError):
        raise DiagnosticProjectionError("diagnostic-record-invalid") from None

__all__ = ["DiagnosticByteSnapshot", "DiagnosticCategory", "DiagnosticDirection",
    "DiagnosticLevel", "DiagnosticObservation", "DiagnosticOutcome",
    "DiagnosticProjectionError", "DiagnosticRecord", "DiagnosticSeverity",
    "project_diagnostic_observation"]
