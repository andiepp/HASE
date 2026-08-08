from google.protobuf.descriptor import FileDescriptor

import hase
from hase._generated import runtime_host_remote_api_v1_pb2


CONTRACT: FileDescriptor = runtime_host_remote_api_v1_pb2.DESCRIPTOR


def _field_numbers(message_name: str) -> dict[str, int]:
    message = CONTRACT.message_types_by_name[message_name]
    return {field.name: field.number for field in message.fields}


def _enum_values(enum_name: str) -> dict[str, int]:
    enum = CONTRACT.enum_types_by_name[enum_name]
    return {value.name: value.number for value in enum.values}


def test_service_has_exact_rpc_shapes() -> None:
    service = CONTRACT.services_by_name["RuntimeHostRemoteApi"]
    actual = {
        method.name: (
            method.input_type.full_name,
            method.output_type.full_name,
            method.client_streaming,
            method.server_streaming,
        )
        for method in service.methods
    }

    assert actual == {
        "GetSnapshot": (
            "hase.runtime.remote.v1.GetSnapshotRequest",
            "hase.runtime.remote.v1.GetSnapshotResponse",
            False,
            False,
        ),
        "ReadCachedProperty": (
            "hase.runtime.remote.v1.ReadCachedPropertyRequest",
            "hase.runtime.remote.v1.CachedPropertyResult",
            False,
            False,
        ),
        "ReadAuthoritativeProperty": (
            "hase.runtime.remote.v1.ReadAuthoritativePropertyRequest",
            "hase.runtime.remote.v1.PropertyOperationResult",
            False,
            False,
        ),
        "WriteProperty": (
            "hase.runtime.remote.v1.WritePropertyRequest",
            "hase.runtime.remote.v1.PropertyOperationResult",
            False,
            False,
        ),
        "ExecuteCommand": (
            "hase.runtime.remote.v1.ExecuteCommandRequest",
            "hase.runtime.remote.v1.CommandOperationResult",
            False,
            False,
        ),
        "Observe": (
            "hase.runtime.remote.v1.ObserveRequest",
            "hase.runtime.remote.v1.ObserveResponse",
            False,
            True,
        ),
        "ObserveDiagnostics": (
            "hase.runtime.remote.v1.ObserveDiagnosticsRequest",
            "hase.runtime.remote.v1.ProjectedDiagnosticObservation",
            False,
            True,
        ),
    }


def test_remote_value_is_the_exact_closed_value_union() -> None:
    remote_value = CONTRACT.message_types_by_name["RemoteValue"]
    kind = remote_value.oneofs_by_name["kind"]

    assert {field.name: field.number for field in kind.fields} == {
        "boolean_value": 1,
        "string_value": 2,
        "numeric_value": 3,
        "byte_array_value": 4,
    }
    assert len(remote_value.oneofs) == 1
    assert len(remote_value.fields) == 4


def test_operation_targets_retain_generation_qualified_identity() -> None:
    assert _field_numbers("PropertyTarget") == {
        "endpoint_id": 1,
        "attachment_generation": 2,
        "instrument_id": 3,
        "property_id": 4,
    }
    assert _field_numbers("CommandTarget") == {
        "endpoint_id": 1,
        "attachment_generation": 2,
        "instrument_id": 3,
        "command_path_segments": 4,
    }


def test_observation_unions_retain_their_exact_shapes() -> None:
    observe_response = CONTRACT.message_types_by_name["ObserveResponse"]
    content = observe_response.oneofs_by_name["content"]
    assert {field.name: field.number for field in content.fields} == {
        "initial_snapshot": 1,
        "observation": 2,
    }

    observation = CONTRACT.message_types_by_name["RuntimeHostObservation"]
    payload = observation.oneofs_by_name["payload"]
    assert {field.name: field.number for field in payload.fields} == {
        "attachment_published": 5,
        "attachment_ended": 6,
        "connection_status_changed": 7,
        "property_value_changed": 8,
        "event_occurred": 9,
    }


def test_command_argument_event_payload_and_duration_types_remain_present() -> None:
    command_argument = CONTRACT.message_types_by_name[
        "CommandDescriptor"
    ].fields_by_name["argument"]
    assert command_argument.number == 4
    assert command_argument.message_type.full_name == (
        "hase.runtime.remote.v1.CommandArgumentDescriptor"
    )

    event_payload = CONTRACT.message_types_by_name[
        "EventDescriptor"
    ].fields_by_name["payload"]
    assert event_payload.number == 4
    assert event_payload.message_type.full_name == (
        "hase.runtime.remote.v1.EventPayloadDescriptor"
    )

    diagnostic_duration = CONTRACT.message_types_by_name[
        "ProjectedDiagnosticRecord"
    ].fields_by_name["duration"]
    assert diagnostic_duration.number == 12
    assert diagnostic_duration.message_type.full_name == "google.protobuf.Duration"


def test_operational_enums_retain_exact_numeric_assignments() -> None:
    assert _enum_values("PropertyOperationStatus") == {
        "PROPERTY_OPERATION_STATUS_UNSPECIFIED": 0,
        "PROPERTY_OPERATION_STATUS_SUCCESS": 1,
        "PROPERTY_OPERATION_STATUS_ATTACHMENT_NOT_CURRENT": 2,
        "PROPERTY_OPERATION_STATUS_INSTRUMENT_NOT_FOUND": 3,
        "PROPERTY_OPERATION_STATUS_PROPERTY_NOT_FOUND": 4,
        "PROPERTY_OPERATION_STATUS_READ_NOT_SUPPORTED": 5,
        "PROPERTY_OPERATION_STATUS_WRITE_NOT_SUPPORTED": 6,
        "PROPERTY_OPERATION_STATUS_INVALID_VALUE": 7,
        "PROPERTY_OPERATION_STATUS_ENDPOINT_UNAVAILABLE": 8,
        "PROPERTY_OPERATION_STATUS_ENDPOINT_REJECTED": 9,
        "PROPERTY_OPERATION_STATUS_ENDPOINT_FAILURE": 10,
        "PROPERTY_OPERATION_STATUS_TIMED_OUT": 11,
    }
    assert _enum_values("CommandOperationStatus") == {
        "COMMAND_OPERATION_STATUS_UNSPECIFIED": 0,
        "COMMAND_OPERATION_STATUS_SUCCESS": 1,
        "COMMAND_OPERATION_STATUS_ATTACHMENT_NOT_CURRENT": 2,
        "COMMAND_OPERATION_STATUS_INSTRUMENT_NOT_FOUND": 3,
        "COMMAND_OPERATION_STATUS_COMMAND_NOT_FOUND": 4,
        "COMMAND_OPERATION_STATUS_ARGUMENT_NOT_SUPPORTED": 5,
        "COMMAND_OPERATION_STATUS_ENDPOINT_UNAVAILABLE": 6,
        "COMMAND_OPERATION_STATUS_ENDPOINT_REJECTED": 7,
        "COMMAND_OPERATION_STATUS_ENDPOINT_FAILURE": 8,
        "COMMAND_OPERATION_STATUS_TIMED_OUT": 9,
    }
    assert _enum_values("EndpointConnectionState") == {
        "ENDPOINT_CONNECTION_STATE_UNSPECIFIED": 0,
        "ENDPOINT_CONNECTION_STATE_DISCONNECTED": 1,
        "ENDPOINT_CONNECTION_STATE_CONNECTING": 2,
        "ENDPOINT_CONNECTION_STATE_SYNCHRONIZING": 3,
        "ENDPOINT_CONNECTION_STATE_READY": 4,
        "ENDPOINT_CONNECTION_STATE_RECONNECTING": 5,
        "ENDPOINT_CONNECTION_STATE_FAULTED": 6,
    }
    assert _enum_values("RuntimeHostObservationKind") == {
        "RUNTIME_HOST_OBSERVATION_KIND_UNSPECIFIED": 0,
        "RUNTIME_HOST_OBSERVATION_KIND_ATTACHMENT_PUBLISHED": 1,
        "RUNTIME_HOST_OBSERVATION_KIND_ATTACHMENT_ENDED": 2,
        "RUNTIME_HOST_OBSERVATION_KIND_CONNECTION_STATUS_CHANGED": 3,
        "RUNTIME_HOST_OBSERVATION_KIND_PROPERTY_VALUE_CHANGED": 4,
        "RUNTIME_HOST_OBSERVATION_KIND_EVENT_OCCURRED": 5,
    }


def test_diagnostic_enums_retain_exact_numeric_assignments() -> None:
    assert _enum_values("RuntimeDiagnosticLevel") == {
        "RUNTIME_DIAGNOSTIC_LEVEL_UNSPECIFIED": 0,
        "RUNTIME_DIAGNOSTIC_LEVEL_OPERATIONAL": 1,
        "RUNTIME_DIAGNOSTIC_LEVEL_PROTOCOL": 2,
        "RUNTIME_DIAGNOSTIC_LEVEL_BYTES": 3,
    }
    assert _enum_values("RuntimeDiagnosticCategory") == {
        "RUNTIME_DIAGNOSTIC_CATEGORY_UNSPECIFIED": 0,
        "RUNTIME_DIAGNOSTIC_CATEGORY_RUNTIME_ATTACHMENT": 1,
        "RUNTIME_DIAGNOSTIC_CATEGORY_RUNTIME_CONNECTION": 2,
        "RUNTIME_DIAGNOSTIC_CATEGORY_RUNTIME_SYNCHRONIZATION": 3,
        "RUNTIME_DIAGNOSTIC_CATEGORY_RUNTIME_RECOVERY": 4,
        "RUNTIME_DIAGNOSTIC_CATEGORY_RUNTIME_PROPERTY": 5,
        "RUNTIME_DIAGNOSTIC_CATEGORY_RUNTIME_COMMAND": 6,
        "RUNTIME_DIAGNOSTIC_CATEGORY_RUNTIME_EVENT": 7,
        "RUNTIME_DIAGNOSTIC_CATEGORY_PROTOCOL_EXCHANGE": 8,
        "RUNTIME_DIAGNOSTIC_CATEGORY_TRANSPORT_BYTES": 9,
    }
    assert _enum_values("RuntimeDiagnosticSeverity") == {
        "RUNTIME_DIAGNOSTIC_SEVERITY_UNSPECIFIED": 0,
        "RUNTIME_DIAGNOSTIC_SEVERITY_TRACE": 1,
        "RUNTIME_DIAGNOSTIC_SEVERITY_INFORMATION": 2,
        "RUNTIME_DIAGNOSTIC_SEVERITY_WARNING": 3,
        "RUNTIME_DIAGNOSTIC_SEVERITY_ERROR": 4,
    }
    assert _enum_values("RuntimeDiagnosticDirection") == {
        "RUNTIME_DIAGNOSTIC_DIRECTION_UNSPECIFIED": 0,
        "RUNTIME_DIAGNOSTIC_DIRECTION_OUTBOUND": 1,
        "RUNTIME_DIAGNOSTIC_DIRECTION_INBOUND": 2,
    }
    assert _enum_values("RuntimeDiagnosticOutcome") == {
        "RUNTIME_DIAGNOSTIC_OUTCOME_UNSPECIFIED": 0,
        "RUNTIME_DIAGNOSTIC_OUTCOME_SUCCEEDED": 1,
        "RUNTIME_DIAGNOSTIC_OUTCOME_FAILED": 2,
        "RUNTIME_DIAGNOSTIC_OUTCOME_CANCELLED": 3,
        "RUNTIME_DIAGNOSTIC_OUTCOME_TIMED_OUT": 4,
    }


def test_generated_contract_remains_outside_the_public_package_surface() -> None:
    assert "runtime_host_remote_api_v1_pb2" not in hase.__all__
    assert "runtime_host_remote_api_v1_pb2_grpc" not in hase.__all__
