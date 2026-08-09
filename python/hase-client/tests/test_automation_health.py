import asyncio

import pytest

from hase import _automation_health as health
from hase.client import RuntimeHostClientError


def test_main_rejects_missing_profile(capsys) -> None:
    assert health.main(()) == 1
    assert capsys.readouterr().err == (
        "HASE automation health failed: arguments-invalid.\n"
    )


def test_main_prints_only_fixed_summary(monkeypatch, capsys) -> None:
    async def run(profile_path: str) -> health._HealthSummary:
        assert profile_path == "external-profile.json"
        return health._HealthSummary(3, 2, 4, 5, 6, 7)

    monkeypatch.setattr(health, "_run", run)
    assert health.main(("external-profile.json",)) == 0
    assert capsys.readouterr().out.splitlines() == [
        "Profile loaded        : True",
        "Channel ready         : True",
        "Snapshot valid        : True",
        "Endpoint count        : 3",
        "Ready endpoint count  : 2",
        "Instrument count      : 4",
        "Property count        : 5",
        "Command count         : 6",
        "Event count           : 7",
        "Channel closed        : True",
        "Workflow succeeded    : True",
    ]


def test_main_sanitizes_known_failure(monkeypatch, capsys) -> None:
    async def fail(profile_path: str) -> health._HealthSummary:
        raise RuntimeHostClientError("rpc-unavailable")

    monkeypatch.setattr(health, "_run", fail)
    assert health.main(("profile.json",)) == 1
    assert capsys.readouterr().err == (
        "HASE automation health failed: rpc-unavailable.\n"
    )


def test_main_sanitizes_unexpected_failure(monkeypatch, capsys) -> None:
    async def fail(profile_path: str) -> health._HealthSummary:
        raise ValueError("must-not-escape")

    monkeypatch.setattr(health, "_run", fail)
    assert health.main(("profile.json",)) == 1
    assert capsys.readouterr().err == (
        "HASE automation health failed: unexpected-failure.\n"
    )


def test_main_propagates_keyboard_interrupt(monkeypatch) -> None:
    async def interrupt(profile_path: str) -> health._HealthSummary:
        raise KeyboardInterrupt

    monkeypatch.setattr(health, "_run", interrupt)
    with pytest.raises(KeyboardInterrupt):
        health.main(("profile.json",))
