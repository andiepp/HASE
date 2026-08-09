import math

import pytest

import hase.mutation as mutation_module
from hase import MutationFailureClassification
from hase import RuntimeHostMutationError
from hase import normalize_mutation_value


@pytest.mark.parametrize(
    ("value", "expected", "expected_type"),
    [
        (False, False, bool),
        (True, True, bool),
        ("", "", str),
        ("CC", "CC", str),
        (b"", b"", bytes),
        (b"\x00\xff\x0d\x0a", b"\x00\xff\x0d\x0a", bytes),
        (0, 0.0, float),
        (42, 42.0, float),
        (-(2**53), float(-(2**53)), float),
        (2**53, float(2**53), float),
        (-0.0, -0.0, float),
        (1.25, 1.25, float),
    ],
)
def test_normalize_mutation_value_preserves_closed_supported_set(
    value: object,
    expected: object,
    expected_type: type,
) -> None:
    result = normalize_mutation_value(value)

    assert type(result) is expected_type
    assert result == expected
    if value == 0.0 and type(value) is float:
        assert math.copysign(1.0, result) == math.copysign(1.0, value)


@pytest.mark.parametrize(
    ("value", "code"),
    [
        (None, "mutation-value-absent"),
        (math.inf, "mutation-number-invalid"),
        (-math.inf, "mutation-number-invalid"),
        (math.nan, "mutation-number-invalid"),
        (2**53 + 1, "mutation-number-not-exact"),
        (10**1000, "mutation-number-invalid"),
        (bytearray(b"secret"), "mutation-value-type-unsupported"),
        (memoryview(b"secret"), "mutation-value-type-unsupported"),
        ([], "mutation-value-type-unsupported"),
        ({}, "mutation-value-type-unsupported"),
    ],
)
def test_normalize_mutation_value_rejects_before_transport(
    value: object,
    code: str,
) -> None:
    with pytest.raises(RuntimeHostMutationError) as failure:
        normalize_mutation_value(value)

    assert failure.value.code == code
    assert failure.value.classification is MutationFailureClassification.NOT_SENT
    assert not failure.value.outcome_uncertain
    assert not failure.value.automatic_retry_permitted
    assert "secret" not in str(failure.value)


@pytest.mark.parametrize(
    ("value", "kind", "wire_value"),
    [
        (True, "boolean_value", True),
        ("", "string_value", ""),
        (1.25, "numeric_value", 1.25),
        (42, "numeric_value", 42.0),
        (b"\x00\xff", "byte_array_value", b"\x00\xff"),
    ],
)
def test_internal_encoder_sets_exactly_one_wire_variant(
    value: object,
    kind: str,
    wire_value: object,
) -> None:
    encoded = mutation_module._encode_mutation_value(value)

    assert encoded.WhichOneof("kind") == kind
    assert getattr(encoded, kind) == wire_value


@pytest.mark.parametrize(
    "classification",
    list(MutationFailureClassification),
)
def test_mutation_error_exposes_only_stable_metadata_and_never_allows_retry(
    classification: MutationFailureClassification,
) -> None:
    failure = RuntimeHostMutationError("stable-code", classification)

    assert failure.code == "stable-code"
    assert failure.classification is classification
    assert failure.outcome_uncertain == (
        classification is MutationFailureClassification.OUTCOME_UNCERTAIN
    )
    assert not failure.automatic_retry_permitted
    assert str(failure) == "Runtime Host mutation failed: stable-code."


@pytest.mark.parametrize(
    ("code", "classification"),
    [
        ("", MutationFailureClassification.NOT_SENT),
        (" whitespace ", MutationFailureClassification.REJECTED),
        ("code", "outcome-uncertain"),
        (None, MutationFailureClassification.NOT_SENT),
    ],
)
def test_mutation_error_rejects_invalid_metadata(
    code: object,
    classification: object,
) -> None:
    with pytest.raises(ValueError) as failure:
        RuntimeHostMutationError(code, classification)  # type: ignore[arg-type]
    assert str(failure.value) == "Invalid Runtime Host mutation error metadata."
