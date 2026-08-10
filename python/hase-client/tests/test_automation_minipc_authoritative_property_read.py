import pytest

from hase import _automation_minipc_authoritative_property_read as workflow
from hase.client import RuntimeHostClientError


def test_main_rejects_missing_profile(capsys) -> None:
    assert workflow.main(()) == 1
    assert capsys.readouterr().err == (
        "HASE MiniPC authoritative read failed: arguments-invalid.\n"
    )


def test_main_runs_once_and_prints_only_fixed_outcomes(monkeypatch, capsys) -> None:
    calls: list[str] = []

    async def validate(profile_path: str) -> None:
        calls.append(profile_path)

    monkeypatch.setattr(workflow, "_validate", validate)

    assert workflow.main(("external-profile.json",)) == 0
    assert calls == ["external-profile.json"]
    assert capsys.readouterr().out.splitlines() == [
        "Profile loaded       : True",
        "Channel ready        : True",
        "A0 target resolved   : True",
        "Read completed       : True",
        "Result valid         : True",
        "Channel closed       : True",
        "Workflow succeeded   : True",
    ]


def test_main_sanitizes_known_failure(monkeypatch, capsys) -> None:
    async def fail(profile_path: str) -> None:
        raise RuntimeHostClientError("rpc-authorization-failed")

    monkeypatch.setattr(workflow, "_validate", fail)

    assert workflow.main(("external-profile.json",)) == 1
    assert capsys.readouterr().err == (
        "HASE MiniPC authoritative read failed: rpc-authorization-failed.\n"
    )


def test_main_sanitizes_unexpected_failure(monkeypatch, capsys) -> None:
    async def fail(profile_path: str) -> None:
        raise ValueError("must-not-escape")

    monkeypatch.setattr(workflow, "_validate", fail)

    assert workflow.main(("secret-profile.json",)) == 1
    output = capsys.readouterr()
    assert output.err == (
        "HASE MiniPC authoritative read failed: unexpected-failure.\n"
    )
    assert "secret-profile" not in output.err


def test_main_propagates_keyboard_interrupt(monkeypatch) -> None:
    async def interrupt(profile_path: str) -> None:
        raise KeyboardInterrupt

    monkeypatch.setattr(workflow, "_validate", interrupt)

    with pytest.raises(KeyboardInterrupt):
        workflow.main(("profile.json",))
