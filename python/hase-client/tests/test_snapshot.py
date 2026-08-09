from dataclasses import FrozenInstanceError
from datetime import datetime, timezone
import math

import pytest

from hase import BooleanDataDescriptor
from hase import ByteArrayDataDescriptor
from hase import EndpointConnectionState
from hase import NumericDataDescriptor
from hase import PropertyAccessMode
from hase import SnapshotProjectionError
from hase import StringDataDescriptor
from hase import project_runtime_host_snapshot
from hase._generated import runtime_host_remote_api_v1_pb2 as contract


def _data(kind: str) -> contract.DataDescriptor:
    result = contract.DataDescriptor()
    if kind == "numeric":
        result.numeric.quantity.id = "electric-current"
        result.numeric.quantity.display_name = "Electric current"
        result.numeric.native_unit.id = "ampere"
        result.numeric.native_unit.display_name = "Ampere"
        result.numeric.native_unit.symbol = "A"
        result.numeric.native_unit.quantity.id = "electric-current"
        result.numeric.native_unit.quantity.display_name = "Electric current"
        result.numeric.range.minimum = 0.0
        result.numeric.range.maximum = 30.0
        result.numeric.resolution.value = 0.001
    else:
        getattr(result, kind).SetInParent()
    return result


def _response() -> contract.GetSnapshotResponse:
    response = contract.GetSnapshotResponse(runtime_host_id="desktop-runtime-host")
    response.api_version.major = 1
    response.api_version.minor = 2

    endpoint = response.endpoints.add(
        endpoint_id="kel-103",
        attachment_generation="attachment-7",
    )
    endpoint.descriptor.endpoint_id = "kel-103"
    endpoint.descriptor.display_name = "KEL-103"
    endpoint.descriptor.description = "Electronic load"
    endpoint.connection_status.state = contract.ENDPOINT_CONNECTION_STATE_READY
    endpoint.connection_status.changed_at_utc.FromDatetime(
        datetime(2026, 8, 9, 4, 5, 6, 7000, tzinfo=timezone.utc)
    )
    endpoint.connection_status.detail = ""

    instrument = endpoint.descriptor.instruments.add(
        instrument_id="load",
        name="Electronic Load",
        kind="electronic-load",
    )
    instrument.manufacturer = "Maynuo"
    instrument.model = "M9711"

    for index, kind in enumerate(
        ("numeric", "boolean_descriptor", "string_descriptor", "byte_array_descriptor")
    ):
        prop = instrument.properties.add(
            property_id=f"property-{index}",
            path_segments=("measurements", f"value-{index}"),
            display_name=f"Property {index}",
            access_mode=contract.PROPERTY_ACCESS_MODE_READ,
        )
        prop.data.CopyFrom(_data(kind))
    instrument.properties[0].description = "Current"

    command = instrument.commands.add(
        path_segments=("output", "set"),
        display_name="Set output",
    )
    command.argument.display_name = "Enabled"
    command.argument.description = "Desired output state"
    command.argument.data.CopyFrom(_data("boolean_descriptor"))

    event = instrument.events.add(
        path_segments=("protection", "changed"),
        display_name="Protection changed",
    )
    event.description = "Protection notification"
    event.payload.display_name = "State"
    event.payload.data.CopyFrom(_data("string_descriptor"))
    return response


def test_projection_preserves_complete_ordered_snapshot() -> None:
    projected = project_runtime_host_snapshot(_response())

    assert projected.runtime_host_id == "desktop-runtime-host"
    assert (projected.api_version.major, projected.api_version.minor) == (1, 2)
    assert len(projected.endpoints) == 1
    endpoint = projected.endpoints[0]
    assert endpoint.endpoint_id == "kel-103"
    assert endpoint.attachment_generation == "attachment-7"
    assert endpoint.descriptor.display_name == "KEL-103"
    assert endpoint.descriptor.description == "Electronic load"
    assert endpoint.connection_status.state is EndpointConnectionState.READY
    assert endpoint.connection_status.changed_at_utc == datetime(
        2026, 8, 9, 4, 5, 6, 7000, tzinfo=timezone.utc
    )
    assert endpoint.connection_status.detail == ""

    instrument = endpoint.descriptor.instruments[0]
    assert instrument.instrument_id == "load"
    assert instrument.manufacturer == "Maynuo"
    assert instrument.model == "M9711"
    assert instrument.serial_number is None
    assert tuple(value.property_id for value in instrument.properties) == (
        "property-0",
        "property-1",
        "property-2",
        "property-3",
    )
    assert instrument.properties[0].description == "Current"
    assert instrument.properties[1].description is None
    assert all(
        value.access_mode is PropertyAccessMode.READ
        for value in instrument.properties
    )
    assert isinstance(instrument.properties[0].data, NumericDataDescriptor)
    assert isinstance(instrument.properties[1].data, BooleanDataDescriptor)
    assert isinstance(instrument.properties[2].data, StringDataDescriptor)
    assert isinstance(instrument.properties[3].data, ByteArrayDataDescriptor)

    numeric = instrument.properties[0].data
    assert numeric.quantity.id == "electric-current"
    assert numeric.native_unit.symbol == "A"
    assert numeric.native_unit.quantity == numeric.quantity
    assert (numeric.value_range.minimum, numeric.value_range.maximum) == (0.0, 30.0)
    assert numeric.resolution == 0.001
    assert instrument.commands[0].path_segments == ("output", "set")
    assert isinstance(instrument.commands[0].argument.data, BooleanDataDescriptor)
    assert instrument.events[0].path_segments == ("protection", "changed")
    assert isinstance(instrument.events[0].payload.data, StringDataDescriptor)


def test_projection_is_deeply_immutable_and_detached() -> None:
    response = _response()
    projected = project_runtime_host_snapshot(response)
    response.runtime_host_id = "substituted"
    response.endpoints[0].descriptor.instruments[0].name = "substituted"

    assert projected.runtime_host_id == "desktop-runtime-host"
    assert projected.endpoints[0].descriptor.instruments[0].name == "Electronic Load"
    with pytest.raises(FrozenInstanceError):
        projected.runtime_host_id = "changed"  # type: ignore[misc]
    with pytest.raises(TypeError):
        projected.endpoints[0] = projected.endpoints[0]  # type: ignore[index]


def test_projection_preserves_authoritative_absent_nested_values() -> None:
    response = _response()
    instrument = response.endpoints[0].descriptor.instruments[0]
    response.endpoints[0].connection_status.ClearField("changed_at_utc")
    instrument.properties[0].data.numeric.ClearField("range")
    instrument.properties[0].data.numeric.ClearField("resolution")
    instrument.commands[0].ClearField("argument")
    instrument.events[0].ClearField("payload")

    projected = project_runtime_host_snapshot(response)
    endpoint = projected.endpoints[0]
    projected_instrument = endpoint.descriptor.instruments[0]
    numeric = projected_instrument.properties[0].data

    assert endpoint.connection_status.changed_at_utc is None
    assert isinstance(numeric, NumericDataDescriptor)
    assert numeric.value_range is None
    assert numeric.resolution is None
    assert projected_instrument.commands[0].argument is None
    assert projected_instrument.events[0].payload is None


@pytest.mark.parametrize(
    ("mutate", "code"),
    [
        (lambda value: setattr(value, "runtime_host_id", ""), "snapshot-text-invalid"),
        (lambda value: value.ClearField("api_version"), "snapshot-message-missing"),
        (
            lambda value: setattr(value.endpoints[0], "attachment_generation", " bad "),
            "snapshot-text-invalid",
        ),
        (
            lambda value: setattr(value.endpoints[0].descriptor, "endpoint_id", "other"),
            "snapshot-endpoint-id-mismatch",
        ),
        (
            lambda value: setattr(value.endpoints[0].connection_status, "state", 0),
            "snapshot-connection-state-invalid",
        ),
        (
            lambda value: setattr(
                value.endpoints[0].descriptor.instruments[0].properties[0],
                "access_mode",
                99,
            ),
            "snapshot-access-mode-invalid",
        ),
        (
            lambda value: value.endpoints[0]
            .descriptor.instruments[0]
            .properties[0]
            .ClearField("data"),
            "snapshot-message-missing",
        ),
        (
            lambda value: value.endpoints[0]
            .descriptor.instruments[0]
            .properties[1]
            .data.Clear(),
            "snapshot-data-kind-invalid",
        ),
        (
            lambda value: setattr(
                value.endpoints[0]
                .descriptor.instruments[0]
                .properties[0]
                .data.numeric.range,
                "minimum",
                31.0,
            ),
            "snapshot-range-invalid",
        ),
        (
            lambda value: setattr(
                value.endpoints[0]
                .descriptor.instruments[0]
                .properties[0]
                .data.numeric.resolution,
                "value",
                0.0,
            ),
            "snapshot-resolution-invalid",
        ),
        (
            lambda value: setattr(
                value.endpoints[0]
                .descriptor.instruments[0]
                .properties[0]
                .data.numeric.range,
                "maximum",
                math.inf,
            ),
            "snapshot-number-invalid",
        ),
    ],
)
def test_projection_rejects_malformed_contract_values(mutate, code: str) -> None:
    response = _response()
    mutate(response)

    with pytest.raises(SnapshotProjectionError) as failure:
        project_runtime_host_snapshot(response)
    assert failure.value.code == code
    assert "kel-103" not in str(failure.value)


def test_projection_rejects_wrong_transport_type_without_details() -> None:
    with pytest.raises(SnapshotProjectionError) as failure:
        project_runtime_host_snapshot(object())  # type: ignore[arg-type]
    assert failure.value.code == "snapshot-type-invalid"
    assert str(failure.value) == (
        "Runtime Host snapshot projection failed: snapshot-type-invalid."
    )
