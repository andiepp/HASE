import pytest

from hase import _automation_same_state_cc_command as workflow
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
        "HASE same-state Command workflow failed: confirmation-required.\n"
    )


def test_confirmed_workflow_uses_reviewed_cc_command_boundary(
    monkeypatch, capsys
) -> None:
    calls = []

    async def execute(profile_path: str) -> None:
        calls.append(profile_path)

    monkeypatch.setattr(workflow, "validate", execute)
    assert workflow.main(("external.json", workflow._CONFIRMATION)) == 0
    assert calls == ["external.json"]
    assert capsys.readouterr().out.splitlines() == [
        "Profile loaded              : True",
        "Safe KEL-103 state verified : True",
        "CC command executed once    : True",
        "CC/OFF reconciliation exact : True",
        "Channel closed              : True",
        "Workflow succeeded          : True",
    ]


def test_uncertain_outcome_stops_after_single_boundary_call(
    monkeypatch, capsys
) -> None:
    calls = 0

    async def execute(profile_path: str) -> None:
        nonlocal calls
        calls += 1
        raise RuntimeHostMutationError(
            "mutation-command-outcome-uncertain",
            MutationFailureClassification.OUTCOME_UNCERTAIN,
        )

    monkeypatch.setattr(workflow, "validate", execute)
    assert workflow.main(("external.json", workflow._CONFIRMATION)) == 1
    assert calls == 1
    assert capsys.readouterr().err == (
        "HASE same-state Command workflow failed: "
        "mutation-command-outcome-uncertain.\n"
    )


def test_unexpected_failure_is_sanitized(monkeypatch, capsys) -> None:
    async def execute(profile_path: str) -> None:
        raise ValueError("must-not-escape")

    monkeypatch.setattr(workflow, "validate", execute)
    assert workflow.main(("external.json", workflow._CONFIRMATION)) == 1
    assert capsys.readouterr().err == (
        "HASE same-state Command workflow failed: unexpected-failure.\n"
    )


def test_keyboard_interrupt_propagates(monkeypatch) -> None:
    async def execute(profile_path: str) -> None:
        raise KeyboardInterrupt

    monkeypatch.setattr(workflow, "validate", execute)
    with pytest.raises(KeyboardInterrupt):
        workflow.main(("external.json", workflow._CONFIRMATION))
