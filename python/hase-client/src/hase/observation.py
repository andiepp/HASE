"""Typed Runtime Host observation stream projections."""
from __future__ import annotations
from dataclasses import dataclass
from datetime import datetime, timezone
from enum import Enum
from typing import TypeAlias
from hase._generated import runtime_host_remote_api_v1_pb2 as contract
from hase.property import PropertyScalar, PropertyValue, _property_value, _remote_value
from hase.snapshot import (EndpointConnectionStatus, RuntimeEndpointSnapshot,
    RuntimeHostSnapshot, _endpoint, _status, project_runtime_host_snapshot)

class ObservationProjectionError(ValueError):
    def __init__(self, code: str):
        self.code = code
        super().__init__(f"Runtime Host observation projection failed: {code}.")

class ObservationKind(Enum):
    ATTACHMENT_PUBLISHED = "attachment-published"
    ATTACHMENT_ENDED = "attachment-ended"
    CONNECTION_STATUS_CHANGED = "connection-status-changed"
    PROPERTY_VALUE_CHANGED = "property-value-changed"
    EVENT_OCCURRED = "event-occurred"

@dataclass(frozen=True, slots=True)
class ObservationInitialSnapshot:
    snapshot: RuntimeHostSnapshot
    snapshot_sequence: int

@dataclass(frozen=True, slots=True)
class AttachmentPublished:
    endpoint: RuntimeEndpointSnapshot

@dataclass(frozen=True, slots=True)
class AttachmentEnded:
    ended_at_utc: datetime

@dataclass(frozen=True, slots=True)
class ConnectionStatusChanged:
    previous_status: EndpointConnectionStatus
    current_status: EndpointConnectionStatus

@dataclass(frozen=True, slots=True)
class PropertyValueChanged:
    instrument_id: str
    property_id: str
    previous_value: PropertyValue
    current_value: PropertyValue

@dataclass(frozen=True, slots=True)
class EventOccurred:
    instrument_id: str
    event_path_segments: tuple[str, ...]
    occurred_at_utc: datetime
    value: PropertyScalar

ObservationPayload: TypeAlias = (AttachmentPublished | AttachmentEnded |
    ConnectionStatusChanged | PropertyValueChanged | EventOccurred)

@dataclass(frozen=True, slots=True)
class RuntimeHostObservation:
    sequence: int
    endpoint_id: str
    attachment_generation: str
    kind: ObservationKind
    payload: ObservationPayload

ObservationMessage: TypeAlias = ObservationInitialSnapshot | RuntimeHostObservation

def _text(value: str) -> str:
    if not isinstance(value, str) or not value or value != value.strip():
        raise ObservationProjectionError("observation-text-invalid")
    return value

def _timestamp(value, code: str) -> datetime:
    try: result = value.ToDatetime(tzinfo=timezone.utc)
    except (OverflowError, ValueError): raise ObservationProjectionError(code) from None
    return result

_KINDS = {
    contract.RUNTIME_HOST_OBSERVATION_KIND_ATTACHMENT_PUBLISHED:
        (ObservationKind.ATTACHMENT_PUBLISHED, "attachment_published"),
    contract.RUNTIME_HOST_OBSERVATION_KIND_ATTACHMENT_ENDED:
        (ObservationKind.ATTACHMENT_ENDED, "attachment_ended"),
    contract.RUNTIME_HOST_OBSERVATION_KIND_CONNECTION_STATUS_CHANGED:
        (ObservationKind.CONNECTION_STATUS_CHANGED, "connection_status_changed"),
    contract.RUNTIME_HOST_OBSERVATION_KIND_PROPERTY_VALUE_CHANGED:
        (ObservationKind.PROPERTY_VALUE_CHANGED, "property_value_changed"),
    contract.RUNTIME_HOST_OBSERVATION_KIND_EVENT_OCCURRED:
        (ObservationKind.EVENT_OCCURRED, "event_occurred"),
}

def project_observe_response(source: contract.ObserveResponse) -> ObservationMessage:
    if not isinstance(source, contract.ObserveResponse):
        raise ObservationProjectionError("observation-response-type-invalid")
    content = source.WhichOneof("content")
    try:
        if content == "initial_snapshot":
            item = source.initial_snapshot
            if not item.HasField("snapshot"):
                raise ObservationProjectionError("observation-snapshot-missing")
            return ObservationInitialSnapshot(
                project_runtime_host_snapshot(item.snapshot), int(item.snapshot_sequence))
        if content != "observation":
            raise ObservationProjectionError("observation-content-invalid")
        item = source.observation
        if item.sequence <= 0: raise ObservationProjectionError("observation-sequence-invalid")
        endpoint_id = _text(item.endpoint_id)
        generation = _text(item.attachment_generation)
        kind, expected_payload = _KINDS[item.kind]
        if item.WhichOneof("payload") != expected_payload:
            raise ObservationProjectionError("observation-payload-invalid")
        payload = getattr(item, expected_payload)
        if kind is ObservationKind.ATTACHMENT_PUBLISHED:
            result = AttachmentPublished(_endpoint(payload.endpoint))
        elif kind is ObservationKind.ATTACHMENT_ENDED:
            if not payload.HasField("ended_at_utc"):
                raise ObservationProjectionError("observation-timestamp-missing")
            result = AttachmentEnded(_timestamp(payload.ended_at_utc,
                "observation-timestamp-invalid"))
        elif kind is ObservationKind.CONNECTION_STATUS_CHANGED:
            result = ConnectionStatusChanged(_status(payload.previous_status),
                _status(payload.current_status))
        elif kind is ObservationKind.PROPERTY_VALUE_CHANGED:
            result = PropertyValueChanged(_text(payload.instrument_id),
                _text(payload.property_id), _property_value(payload.previous_value),
                _property_value(payload.current_value))
        else:
            segments = tuple(_text(x) for x in payload.event_path_segments)
            if not segments or not payload.HasField("occurred_at_utc"):
                raise ObservationProjectionError("observation-event-invalid")
            value = _remote_value(payload.value) if payload.HasField("value") else None
            result = EventOccurred(_text(payload.instrument_id), segments,
                _timestamp(payload.occurred_at_utc, "observation-timestamp-invalid"), value)
        return RuntimeHostObservation(int(item.sequence), endpoint_id, generation,
            kind, result)
    except ObservationProjectionError: raise
    except (KeyError, TypeError, ValueError):
        raise ObservationProjectionError("observation-shape-invalid") from None

__all__ = ["AttachmentEnded", "AttachmentPublished", "ConnectionStatusChanged",
    "EventOccurred", "ObservationInitialSnapshot", "ObservationKind",
    "ObservationMessage", "ObservationPayload", "ObservationProjectionError",
    "PropertyValueChanged", "RuntimeHostObservation", "project_observe_response"]
