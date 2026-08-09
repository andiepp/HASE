from datetime import timezone

import pytest

import hase.mutation as mutation_module
from hase import MutationFailureClassification
from hase import PropertyOperationStatus
from hase import PropertyQuality
from hase import RuntimeHostMutationError
from hase._generated import runtime_host_remote_api_v1_pb2 as contract


def _success() -> contract.PropertyOperationResult:
    result = contract.PropertyOperationResult(
        status=contract.PROPERTY_OPERATION_STATUS_SUCCESS
    )
    result.confirmed_value.value.numeric_value = 1.25
    result.confirmed_value.timestamp_utc.FromJsonString(
        "2026-08-09T10:11:12Z"
    )
    result.confirmed_value.quality = contract.PROPERTY_QUALITY_GOOD
    return result


def test_internal_property_mutation_projector_returns_confirmed_success() -> None:
    result = mutation_module._project_property_mutation_result(_success())

    assert result.status is PropertyOperationStatus.SUCCESS
    assert result.is_success
    assert result.diagnostic is None
    assert result.confirmed_value is not None
    assert result.confirmed_value.value == 1.25
    assert result.confirmed_value.quality is PropertyQuality.GOOD
    assert result.confirmed_value.timestamp_utc.tzinfo is timezone.utc


@pytest.mark.parametrize(
    ("status", "code"),
    [
        (
            contract.PROPERTY_OPERATION_STATUS_ATTACHMENT_NOT_CURRENT,
            "mutation-property-attachment-not-current",
        ),
        (
            contract.PROPERTY_OPERATION_STATUS_INSTRUMENT_NOT_FOUND,
            "mutation-property-instrument-not-found",
        ),
        (
            contract.PROPERTY_OPERATION_STATUS_PROPERTY_NOT_FOUND,
            "mutation-property-not-found",
        ),
        (
            contract.PROPERTY_OPERATION_STATUS_WRITE_NOT_SUPPORTED,
            "mutation-property-write-not-supported",
        ),
        (
            contract.PROPERTY_OPERATION_STATUS_INVALID_VALUE,
            "mutation-property-invalid-value",
        ),
        (
            contract.PROPERTY_OPERATION_STATUS_ENDPOINT_UNAVAILABLE,
            "mutation-property-endpoint-unavailable",
        ),
        (
            contract.PROPERTY_OPERATION_STATUS_ENDPOINT_REJECTED,
            "mutation-property-endpoint-rejected",
        ),
    ],
)
def test_internal_property_mutation_projector_classifies_safe_rejection(
    status: int,
    code: str,
) -> None:
    source = contract.PropertyOperationResult(status=status)
    source.diagnostic = "secret host, endpoint, and credential detail"

    with pytest.raises(RuntimeHostMutationError) as captured:
        mutation_module._project_property_mutation_result(source)

    assert captured.value.code == code
    assert captured.value.classification is MutationFailureClassification.REJECTED
    assert not captured.value.outcome_uncertain
    assert not captured.value.automatic_retry_permitted
    assert "secret" not in str(captured.value)


@pytest.mark.parametrize(
    ("status", "code"),
    [
        (
            contract.PROPERTY_OPERATION_STATUS_ENDPOINT_FAILURE,
            "mutation-property-endpoint-failure",
        ),
        (
            contract.PROPERTY_OPERATION_STATUS_TIMED_OUT,
            "mutation-property-timed-out",
        ),
        (
            contract.PROPERTY_OPERATION_STATUS_READ_NOT_SUPPORTED,
            "mutation-property-result-invalid",
        ),
        (
            contract.PROPERTY_OPERATION_STATUS_UNSPECIFIED,
            "mutation-property-result-invalid",
        ),
    ],
)
def test_internal_property_mutation_projector_classifies_uncertain_result(
    status: int,
    code: str,
) -> None:
    source = contract.PropertyOperationResult(status=status)
    source.diagnostic = "secret host, endpoint, and credential detail"

    with pytest.raises(RuntimeHostMutationError) as captured:
        mutation_module._project_property_mutation_result(source)

    assert captured.value.code == code
    assert (
        captured.value.classification
        is MutationFailureClassification.OUTCOME_UNCERTAIN
    )
    assert captured.value.outcome_uncertain
    assert not captured.value.automatic_retry_permitted
    assert "secret" not in str(captured.value)


@pytest.mark.parametrize(
    "source",
    [
        object(),
        contract.PropertyOperationResult(
            status=contract.PROPERTY_OPERATION_STATUS_SUCCESS
        ),
        contract.PropertyOperationResult(
            status=contract.PROPERTY_OPERATION_STATUS_INVALID_VALUE,
            confirmed_value=contract.PropertyValue(),
        ),
    ],
)
def test_internal_property_mutation_projector_classifies_malformed_result_uncertain(
    source: object,
) -> None:
    with pytest.raises(RuntimeHostMutationError) as captured:
        mutation_module._project_property_mutation_result(source)  # type: ignore[arg-type]

    assert captured.value.code == "mutation-property-result-invalid"
    assert (
        captured.value.classification
        is MutationFailureClassification.OUTCOME_UNCERTAIN
    )
    assert captured.value.outcome_uncertain
    assert not captured.value.automatic_retry_permitted


def test_every_version_one_property_status_has_explicit_mutation_semantics() -> None:
    covered = {
        contract.PROPERTY_OPERATION_STATUS_UNSPECIFIED,
        contract.PROPERTY_OPERATION_STATUS_SUCCESS,
        contract.PROPERTY_OPERATION_STATUS_ATTACHMENT_NOT_CURRENT,
        contract.PROPERTY_OPERATION_STATUS_INSTRUMENT_NOT_FOUND,
        contract.PROPERTY_OPERATION_STATUS_PROPERTY_NOT_FOUND,
        contract.PROPERTY_OPERATION_STATUS_READ_NOT_SUPPORTED,
        contract.PROPERTY_OPERATION_STATUS_WRITE_NOT_SUPPORTED,
        contract.PROPERTY_OPERATION_STATUS_INVALID_VALUE,
        contract.PROPERTY_OPERATION_STATUS_ENDPOINT_UNAVAILABLE,
        contract.PROPERTY_OPERATION_STATUS_ENDPOINT_REJECTED,
        contract.PROPERTY_OPERATION_STATUS_ENDPOINT_FAILURE,
        contract.PROPERTY_OPERATION_STATUS_TIMED_OUT,
    }

    assert covered == set(contract.PropertyOperationStatus.values())
