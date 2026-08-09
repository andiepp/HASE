import pytest

from hase import _automation_same_value_property_write as workflow
from hase.mutation import (
    MutationFailureClassification,
    RuntimeHostMutationError,
)


@pytest.mark.parametrize(
    "arguments",
    [(), ("profile",), ("profile", "wrong"), ("one", "two", "three")],
)
def test_confirmation_is_required(arguments, capsys) -> None:
    assert workflow.main(arguments) == 1
    assert capsys.readouterr().err == (
        "HASE same-value Property workflow failed: confirmation-required.\n"
    )


def test_confirmed_workflow_uses_reviewed_same_value_boundary(
    monkeypatch, capsys
) -> None:
    calls = []

    async def validate(profile_path: str) -> None:
        calls.append(profile_path)

    monkeypatch.setattr(workflow, "_validate", validate)
    assert workflow.main(("external.json", workflow._CONFIRMATION)) == 0
    assert calls == ["external.json"]
    assert capsys.readouterr().out.splitlines()[-1] == (
        "Workflow succeeded          : True"
    )


def test_uncertain_outcome_is_sanitized_without_second_attempt(
    monkeypatch, capsys
) -> None:
    calls = 0

    async def validate(profile_path: str) -> None:
        nonlocal calls
        calls += 1
        raise RuntimeHostMutationError(
            "mutation-outcome-uncertain",
            MutationFailureClassification.OUTCOME_UNCERTAIN,
        )

    monkeypatch.setattr(workflow, "_validate", validate)
    assert workflow.main(("external.json", workflow._CONFIRMATION)) == 1
    assert calls == 1
    assert capsys.readouterr().err == (
        "HASE same-value Property workflow failed: "
        "mutation-outcome-uncertain.\n"
    )


def test_unexpected_failure_is_sanitized(monkeypatch, capsys) -> None:
    async def validate(profile_path: str) -> None:
        raise ValueError("must-not-escape")

    monkeypatch.setattr(workflow, "_validate", validate)
    assert workflow.main(("external.json", workflow._CONFIRMATION)) == 1
    assert capsys.readouterr().err == (
        "HASE same-value Property workflow failed: unexpected-failure.\n"
    )


def test_keyboard_interrupt_propagates(monkeypatch) -> None:
    async def validate(profile_path: str) -> None:
        raise KeyboardInterrupt

    monkeypatch.setattr(workflow, "_validate", validate)
    with pytest.raises(KeyboardInterrupt):
        workflow.main(("external.json", workflow._CONFIRMATION))
