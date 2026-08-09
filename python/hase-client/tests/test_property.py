from dataclasses import FrozenInstanceError
from datetime import datetime, timezone
import math

import pytest

from hase import PropertyOperationStatus
from hase import PropertyProjectionError
from hase import PropertyQuality
from hase import PropertyTarget
from hase import project_property_operation_result
from hase import project_property_target
from hase._generated import runtime_host_remote_api_v1_pb2 as contract


def _success(kind: str = "numeric_value") -> contract.PropertyOperationResult:
    result = contract.PropertyOperationResult(
        status=contract.PROPERTY_OPERATION_STATUS_SUCCESS
    )
    value = result.confirmed_value
    value.timestamp_utc.FromDatetime(
        datetime(2026, 8, 9, 10, 11, 12, 13000, tzinfo=timezone.utc)
    )
    value.quality = contract.PROPERTY_QUALITY_GOOD
    if kind == "boolean_value":
        value.value.boolean_value = True
    elif kind == "string_value":
        value.value.string_value = "CC"
    elif kind == "numeric_value":
        value.value.numeric_value = 1.25
    elif kind == "byte_array_value":
        value.value.byte_array_value = b"\x00\xff\x0d\x0a"
    elif kind == "none":
        pass
    else:
        raise AssertionError(kind)
    return result


def test_target_projection_and_public_construction_are_immutable() -> None:
    source = contract.PropertyTarget(
        endpoint_id="kel-103",
        attachment_generation="attachment-7",
        instrument_id="load",
        property_id="measured-current",
    )

    target = project_property_target(source)
    source.endpoint_id = "substituted"

    assert target == PropertyTarget(
        "kel-103",
        "attachment-7",
        "load",
        "measured-current",
    )
    with pytest.raises(FrozenInstanceError):
        target.endpoint_id = "changed"  # type: ignore[misc]


@pytest.mark.parametrize("name", PropertyTarget.__dataclass_fields__)
@pytest.mark.parametrize("value", ["", " whitespace "])
def test_target_rejects_invalid_identity(name: str, value: str) -> None:
    values = {
        "endpoint_id": "endpoint",
        "attachment_generation": "generation",
        "instrument_id": "instrument",
        "property_id": "property",
    }
    values[name] = value

    with pytest.raises(PropertyProjectionError) as failure:
        PropertyTarget(**values)
    assert failure.value.code == "property-text-invalid"


@pytest.mark.parametrize(
    ("kind", "expected"),
    [
        ("boolean_value", True),
        ("string_value", "CC"),
        ("numeric_value", 1.25),
        ("byte_array_value", b"\x00\xff\x0d\x0a"),
        ("none", None),
    ],
)
def test_success_projection_preserves_every_value_variant(
    kind: str,
    expected: object,
) -> None:
    source = _success(kind)

    result = project_property_operation_result(source)

    assert result.status is PropertyOperationStatus.SUCCESS
    assert result.is_success
    assert result.diagnostic is None
    assert result.confirmed_value is not None
    assert result.confirmed_value.value == expected
    assert result.confirmed_value.timestamp_utc == datetime(
        2026, 8, 9, 10, 11, 12, 13000, tzinfo=timezone.utc
    )
    assert result.confirmed_value.quality is PropertyQuality.GOOD
    source.confirmed_value.value.byte_array_value = b"substituted"
    assert result.confirmed_value.value == expected


@pytest.mark.parametrize(
    ("wire_status", "status"),
    [
        (
            contract.PROPERTY_OPERATION_STATUS_ATTACHMENT_NOT_CURRENT,
            PropertyOperationStatus.ATTACHMENT_NOT_CURRENT,
        ),
        (
            contract.PROPERTY_OPERATION_STATUS_INSTRUMENT_NOT_FOUND,
            PropertyOperationStatus.INSTRUMENT_NOT_FOUND,
        ),
        (
            contract.PROPERTY_OPERATION_STATUS_PROPERTY_NOT_FOUND,
            PropertyOperationStatus.PROPERTY_NOT_FOUND,
        ),
        (
            contract.PROPERTY_OPERATION_STATUS_READ_NOT_SUPPORTED,
            PropertyOperationStatus.READ_NOT_SUPPORTED,
        ),
        (
            contract.PROPERTY_OPERATION_STATUS_WRITE_NOT_SUPPORTED,
            PropertyOperationStatus.WRITE_NOT_SUPPORTED,
        ),
        (
            contract.PROPERTY_OPERATION_STATUS_INVALID_VALUE,
            PropertyOperationStatus.INVALID_VALUE,
        ),
        (
            contract.PROPERTY_OPERATION_STATUS_ENDPOINT_UNAVAILABLE,
            PropertyOperationStatus.ENDPOINT_UNAVAILABLE,
        ),
        (
            contract.PROPERTY_OPERATION_STATUS_ENDPOINT_REJECTED,
            PropertyOperationStatus.ENDPOINT_REJECTED,
        ),
        (
            contract.PROPERTY_OPERATION_STATUS_ENDPOINT_FAILURE,
            PropertyOperationStatus.ENDPOINT_FAILURE,
        ),
        (
            contract.PROPERTY_OPERATION_STATUS_TIMED_OUT,
            PropertyOperationStatus.TIMED_OUT,
        ),
    ],
)
def test_failure_projection_preserves_status_and_optional_diagnostic(
    wire_status: int,
    status: PropertyOperationStatus,
) -> None:
    source = contract.PropertyOperationResult(status=wire_status)
    source.diagnostic = ""

    result = project_property_operation_result(source)

    assert result.status is status
    assert not result.is_success
    assert result.confirmed_value is None
    assert result.diagnostic == ""


def test_failure_projection_preserves_absent_diagnostic() -> None:
    result = project_property_operation_result(
        contract.PropertyOperationResult(
            status=contract.PROPERTY_OPERATION_STATUS_PROPERTY_NOT_FOUND
        )
    )
    assert result.diagnostic is None


@pytest.mark.parametrize(
    ("mutate", "code"),
    [
        (
            lambda value: setattr(value, "status", 0),
            "property-status-invalid",
        ),
        (
            lambda value: value.ClearField("confirmed_value"),
            "property-success-shape-invalid",
        ),
        (
            lambda value: setattr(value, "diagnostic", "unexpected"),
            "property-success-shape-invalid",
        ),
        (
            lambda value: value.confirmed_value.ClearField("timestamp_utc"),
            "property-timestamp-missing",
        ),
        (
            lambda value: setattr(value.confirmed_value.timestamp_utc, "seconds", 253402300800),
            "property-timestamp-invalid",
        ),
        (
            lambda value: setattr(value.confirmed_value, "quality", 0),
            "property-quality-invalid",
        ),
        (
            lambda value: setattr(value.confirmed_value.value, "numeric_value", math.inf),
            "property-number-invalid",
        ),
    ],
)
def test_success_projection_rejects_malformed_shape(mutate, code: str) -> None:
    source = _success()
    mutate(source)

    with pytest.raises(PropertyProjectionError) as failure:
        project_property_operation_result(source)
    assert failure.value.code == code
    assert "kel-103" not in str(failure.value)


def test_failure_projection_rejects_confirmed_value() -> None:
    source = _success()
    source.status = contract.PROPERTY_OPERATION_STATUS_ENDPOINT_FAILURE

    with pytest.raises(PropertyProjectionError) as failure:
        project_property_operation_result(source)
    assert failure.value.code == "property-failure-shape-invalid"


def test_projection_rejects_wrong_transport_types_without_details() -> None:
    with pytest.raises(PropertyProjectionError) as target_failure:
        project_property_target(object())  # type: ignore[arg-type]
    assert target_failure.value.code == "property-target-type-invalid"

    with pytest.raises(PropertyProjectionError) as result_failure:
        project_property_operation_result(object())  # type: ignore[arg-type]
    assert result_failure.value.code == "property-result-type-invalid"
