"""Immutable public Runtime Host snapshot models and strict projection."""

from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime, timezone
from enum import Enum
import math
from typing import TypeAlias

from google.protobuf.timestamp_pb2 import Timestamp

from hase._generated import runtime_host_remote_api_v1_pb2 as contract


class SnapshotProjectionError(ValueError):
    """A sanitized failure raised for an invalid transport snapshot."""

    def __init__(self, code: str) -> None:
        self.code = code
        super().__init__(f"Runtime Host snapshot projection failed: {code}.")


class EndpointConnectionState(Enum):
    DISCONNECTED = "disconnected"
    CONNECTING = "connecting"
    SYNCHRONIZING = "synchronizing"
    READY = "ready"
    RECONNECTING = "reconnecting"
    FAULTED = "faulted"


class PropertyAccessMode(Enum):
    NONE = "none"
    READ = "read"
    WRITE = "write"
    READ_WRITE = "read-write"


@dataclass(frozen=True, slots=True)
class RuntimeHostApiVersion:
    major: int
    minor: int


@dataclass(frozen=True, slots=True)
class Quantity:
    id: str
    display_name: str


@dataclass(frozen=True, slots=True)
class Unit:
    id: str
    display_name: str
    symbol: str
    quantity: Quantity


@dataclass(frozen=True, slots=True)
class ValueRange:
    minimum: float
    maximum: float


@dataclass(frozen=True, slots=True)
class NumericDataDescriptor:
    quantity: Quantity
    native_unit: Unit
    value_range: ValueRange | None
    resolution: float | None


@dataclass(frozen=True, slots=True)
class BooleanDataDescriptor:
    pass


@dataclass(frozen=True, slots=True)
class StringDataDescriptor:
    pass


@dataclass(frozen=True, slots=True)
class ByteArrayDataDescriptor:
    pass


DataDescriptor: TypeAlias = (
    NumericDataDescriptor
    | BooleanDataDescriptor
    | StringDataDescriptor
    | ByteArrayDataDescriptor
)


@dataclass(frozen=True, slots=True)
class PropertyDescriptor:
    property_id: str
    path_segments: tuple[str, ...]
    display_name: str
    description: str | None
    access_mode: PropertyAccessMode
    data: DataDescriptor


@dataclass(frozen=True, slots=True)
class CommandArgumentDescriptor:
    display_name: str
    description: str | None
    data: DataDescriptor


@dataclass(frozen=True, slots=True)
class CommandDescriptor:
    path_segments: tuple[str, ...]
    display_name: str
    description: str | None
    argument: CommandArgumentDescriptor | None


@dataclass(frozen=True, slots=True)
class EventPayloadDescriptor:
    display_name: str
    description: str | None
    data: DataDescriptor


@dataclass(frozen=True, slots=True)
class EventDescriptor:
    path_segments: tuple[str, ...]
    display_name: str
    description: str | None
    payload: EventPayloadDescriptor | None


@dataclass(frozen=True, slots=True)
class InstrumentDescriptor:
    instrument_id: str
    name: str
    kind: str
    manufacturer: str | None
    model: str | None
    serial_number: str | None
    firmware_version: str | None
    hardware_revision: str | None
    description: str | None
    properties: tuple[PropertyDescriptor, ...]
    commands: tuple[CommandDescriptor, ...]
    events: tuple[EventDescriptor, ...]


@dataclass(frozen=True, slots=True)
class EndpointDescriptor:
    endpoint_id: str
    display_name: str | None
    description: str | None
    instruments: tuple[InstrumentDescriptor, ...]


@dataclass(frozen=True, slots=True)
class EndpointConnectionStatus:
    state: EndpointConnectionState
    changed_at_utc: datetime | None
    detail: str | None


@dataclass(frozen=True, slots=True)
class RuntimeEndpointSnapshot:
    endpoint_id: str
    attachment_generation: str
    descriptor: EndpointDescriptor
    connection_status: EndpointConnectionStatus


@dataclass(frozen=True, slots=True)
class RuntimeHostSnapshot:
    runtime_host_id: str
    api_version: RuntimeHostApiVersion
    endpoints: tuple[RuntimeEndpointSnapshot, ...]


def _required_text(value: str) -> str:
    if not isinstance(value, str) or not value or value != value.strip():
        raise SnapshotProjectionError("snapshot-text-invalid")
    return value


def _segments(values: object) -> tuple[str, ...]:
    result = tuple(_required_text(value) for value in values)  # type: ignore[arg-type]
    if not result:
        raise SnapshotProjectionError("snapshot-path-invalid")
    return result


def _optional(message: object, name: str) -> str | None:
    return getattr(message, name) if message.HasField(name) else None  # type: ignore[attr-defined]


def _message(message: object, name: str) -> object:
    if not message.HasField(name):  # type: ignore[attr-defined]
        raise SnapshotProjectionError("snapshot-message-missing")
    return getattr(message, name)


def _finite(value: float) -> float:
    result = float(value)
    if not math.isfinite(result):
        raise SnapshotProjectionError("snapshot-number-invalid")
    return result


def _quantity(source: contract.Quantity) -> Quantity:
    return Quantity(_required_text(source.id), _required_text(source.display_name))


def _data(source: contract.DataDescriptor) -> DataDescriptor:
    kind = source.WhichOneof("kind")
    if kind == "boolean_descriptor":
        return BooleanDataDescriptor()
    if kind == "string_descriptor":
        return StringDataDescriptor()
    if kind == "byte_array_descriptor":
        return ByteArrayDataDescriptor()
    if kind != "numeric":
        raise SnapshotProjectionError("snapshot-data-kind-invalid")

    numeric = source.numeric
    quantity = _quantity(_message(numeric, "quantity"))  # type: ignore[arg-type]
    native_unit_source = _message(numeric, "native_unit")
    unit_quantity = _quantity(_message(native_unit_source, "quantity"))  # type: ignore[arg-type]
    value_range = None
    if numeric.HasField("range"):
        minimum = _finite(numeric.range.minimum)
        maximum = _finite(numeric.range.maximum)
        if minimum > maximum:
            raise SnapshotProjectionError("snapshot-range-invalid")
        value_range = ValueRange(minimum, maximum)
    resolution = None
    if numeric.HasField("resolution"):
        resolution = _finite(numeric.resolution.value)
        if resolution <= 0:
            raise SnapshotProjectionError("snapshot-resolution-invalid")

    return NumericDataDescriptor(
        quantity,
        Unit(
            _required_text(native_unit_source.id),  # type: ignore[attr-defined]
            _required_text(native_unit_source.display_name),  # type: ignore[attr-defined]
            _required_text(native_unit_source.symbol),  # type: ignore[attr-defined]
            unit_quantity,
        ),
        value_range,
        resolution,
    )


_ACCESS_MODES = {
    contract.PROPERTY_ACCESS_MODE_NONE: PropertyAccessMode.NONE,
    contract.PROPERTY_ACCESS_MODE_READ: PropertyAccessMode.READ,
    contract.PROPERTY_ACCESS_MODE_WRITE: PropertyAccessMode.WRITE,
    contract.PROPERTY_ACCESS_MODE_READ_WRITE: PropertyAccessMode.READ_WRITE,
}

_CONNECTION_STATES = {
    contract.ENDPOINT_CONNECTION_STATE_DISCONNECTED: EndpointConnectionState.DISCONNECTED,
    contract.ENDPOINT_CONNECTION_STATE_CONNECTING: EndpointConnectionState.CONNECTING,
    contract.ENDPOINT_CONNECTION_STATE_SYNCHRONIZING: EndpointConnectionState.SYNCHRONIZING,
    contract.ENDPOINT_CONNECTION_STATE_READY: EndpointConnectionState.READY,
    contract.ENDPOINT_CONNECTION_STATE_RECONNECTING: EndpointConnectionState.RECONNECTING,
    contract.ENDPOINT_CONNECTION_STATE_FAULTED: EndpointConnectionState.FAULTED,
}


def _property(source: contract.PropertyDescriptor) -> PropertyDescriptor:
    try:
        access_mode = _ACCESS_MODES[source.access_mode]
    except KeyError:
        raise SnapshotProjectionError("snapshot-access-mode-invalid") from None
    return PropertyDescriptor(
        _required_text(source.property_id),
        _segments(source.path_segments),
        _required_text(source.display_name),
        _optional(source, "description"),
        access_mode,
        _data(_message(source, "data")),  # type: ignore[arg-type]
    )


def _command(source: contract.CommandDescriptor) -> CommandDescriptor:
    argument = None
    if source.HasField("argument"):
        argument_source = source.argument
        argument = CommandArgumentDescriptor(
            _required_text(argument_source.display_name),
            _optional(argument_source, "description"),
            _data(_message(argument_source, "data")),  # type: ignore[arg-type]
        )
    return CommandDescriptor(
        _segments(source.path_segments),
        _required_text(source.display_name),
        _optional(source, "description"),
        argument,
    )


def _event(source: contract.EventDescriptor) -> EventDescriptor:
    payload = None
    if source.HasField("payload"):
        payload_source = source.payload
        payload = EventPayloadDescriptor(
            _required_text(payload_source.display_name),
            _optional(payload_source, "description"),
            _data(_message(payload_source, "data")),  # type: ignore[arg-type]
        )
    return EventDescriptor(
        _segments(source.path_segments),
        _required_text(source.display_name),
        _optional(source, "description"),
        payload,
    )


def _instrument(source: contract.InstrumentDescriptor) -> InstrumentDescriptor:
    return InstrumentDescriptor(
        _required_text(source.instrument_id),
        _required_text(source.name),
        _required_text(source.kind),
        _optional(source, "manufacturer"),
        _optional(source, "model"),
        _optional(source, "serial_number"),
        _optional(source, "firmware_version"),
        _optional(source, "hardware_revision"),
        _optional(source, "description"),
        tuple(_property(value) for value in source.properties),
        tuple(_command(value) for value in source.commands),
        tuple(_event(value) for value in source.events),
    )


def _status(source: contract.EndpointConnectionStatus) -> EndpointConnectionStatus:
    try:
        state = _CONNECTION_STATES[source.state]
    except KeyError:
        raise SnapshotProjectionError("snapshot-connection-state-invalid") from None
    changed_at = None
    if source.HasField("changed_at_utc"):
        try:
            changed_at = source.changed_at_utc.ToDatetime(tzinfo=timezone.utc)
        except (OverflowError, ValueError):
            raise SnapshotProjectionError("snapshot-timestamp-invalid") from None
    return EndpointConnectionStatus(state, changed_at, _optional(source, "detail"))


def _endpoint(source: contract.PublishedRuntimeEndpointSnapshot) -> RuntimeEndpointSnapshot:
    descriptor_source = _message(source, "descriptor")
    descriptor = EndpointDescriptor(
        _required_text(descriptor_source.endpoint_id),  # type: ignore[attr-defined]
        _optional(descriptor_source, "display_name"),
        _optional(descriptor_source, "description"),
        tuple(_instrument(value) for value in descriptor_source.instruments),  # type: ignore[attr-defined]
    )
    endpoint_id = _required_text(source.endpoint_id)
    if descriptor.endpoint_id != endpoint_id:
        raise SnapshotProjectionError("snapshot-endpoint-id-mismatch")
    return RuntimeEndpointSnapshot(
        endpoint_id,
        _required_text(source.attachment_generation),
        descriptor,
        _status(_message(source, "connection_status")),  # type: ignore[arg-type]
    )


def project_runtime_host_snapshot(source: contract.GetSnapshotResponse) -> RuntimeHostSnapshot:
    """Project one generated response into an immutable public snapshot."""

    if not isinstance(source, contract.GetSnapshotResponse):
        raise SnapshotProjectionError("snapshot-type-invalid")
    api_version = _message(source, "api_version")
    return RuntimeHostSnapshot(
        _required_text(source.runtime_host_id),
        RuntimeHostApiVersion(api_version.major, api_version.minor),  # type: ignore[attr-defined]
        tuple(_endpoint(value) for value in source.endpoints),
    )


__all__ = [
    "BooleanDataDescriptor",
    "ByteArrayDataDescriptor",
    "CommandArgumentDescriptor",
    "CommandDescriptor",
    "DataDescriptor",
    "EndpointConnectionState",
    "EndpointConnectionStatus",
    "EndpointDescriptor",
    "EventDescriptor",
    "EventPayloadDescriptor",
    "InstrumentDescriptor",
    "NumericDataDescriptor",
    "PropertyAccessMode",
    "PropertyDescriptor",
    "Quantity",
    "RuntimeEndpointSnapshot",
    "RuntimeHostApiVersion",
    "RuntimeHostSnapshot",
    "SnapshotProjectionError",
    "StringDataDescriptor",
    "Unit",
    "ValueRange",
    "project_runtime_host_snapshot",
]
