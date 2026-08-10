from pathlib import Path

import pytest

from hase import _automation_target_selection as selection
from hase import _client_automation_target_readiness as readiness
from hase import AutomationTarget
from hase import AutomationTargetRegistry
from hase import AutomationTargetRegistryError
from hase import RuntimeHostProfile


def _registry(tmp_path: Path) -> AutomationTargetRegistry:
    def target(target_id: str, suffix: str) -> AutomationTarget:
        profile_path = tmp_path / f"{suffix}.json"
        profile_path.write_text("{}", encoding="utf-8")
        profile = RuntimeHostProfile(
            1,
            f"https://192.0.2.{10 if suffix == 'desktop' else 11}:50443",
            tmp_path / f"{suffix}-certificate.pem",
            tmp_path / f"{suffix}-key.pem",
            tmp_path / f"{suffix}-server.cer",
        )
        return AutomationTarget(target_id, suffix, profile_path, profile)

    return AutomationTargetRegistry(
        1,
        (
            target("desktop-runtime-host", "desktop"),
            target("minipc-runtime-host", "minipc"),
        ),
    )


def test_selection_prints_only_explicit_profile_path(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, capsys
) -> None:
    registry = _registry(tmp_path)
    monkeypatch.setattr(selection, "load_automation_target_registry", lambda *a, **k: registry)

    assert selection.main(("registry.json", "minipc-runtime-host", "excluded")) == 0
    output = capsys.readouterr()
    assert output.out.strip() == str(tmp_path / "minipc.json")
    assert output.err == ""


def test_selection_rejects_arguments_and_sanitizes_failures(monkeypatch, capsys) -> None:
    assert selection.main(("registry.json",)) == 1
    assert capsys.readouterr().err.endswith("arguments-invalid.\n")

    def fail(*args, **kwargs):
        raise AutomationTargetRegistryError("registry-json-invalid")

    monkeypatch.setattr(selection, "load_automation_target_registry", fail)
    assert selection.main(("registry.json", "desktop-runtime-host")) == 1
    assert capsys.readouterr().err.endswith("registry-json-invalid.\n")


def test_selection_rejects_unknown_target(tmp_path: Path, monkeypatch, capsys) -> None:
    monkeypatch.setattr(
        selection,
        "load_automation_target_registry",
        lambda *a, **k: _registry(tmp_path),
    )
    assert selection.main(("registry.json", "automatic")) == 1
    assert capsys.readouterr().err.endswith("target-id-unknown.\n")


def test_readiness_prints_only_fixed_outcomes(tmp_path: Path, monkeypatch, capsys) -> None:
    monkeypatch.setattr(
        readiness,
        "load_automation_target_registry",
        lambda *a, **k: _registry(tmp_path),
    )
    assert readiness.main(("registry.json", "repository", "installation")) == 0
    assert capsys.readouterr().out.splitlines() == [
        "Registry loaded          : True",
        "Desktop target ready     : True",
        "MiniPC target ready      : True",
        "Profile custody external : True",
        "Profiles distinct        : True",
        "Credentials distinct     : True",
        "Connection attempted     : False",
        "Laptop targets ready     : True",
    ]


def test_readiness_rejects_arguments_and_sanitizes_failure(monkeypatch, capsys) -> None:
    assert readiness.main(()) == 1
    assert capsys.readouterr().err.endswith("arguments-invalid.\n")

    def fail(*args, **kwargs):
        raise AutomationTargetRegistryError("target-credentials-not-distinct")

    monkeypatch.setattr(readiness, "load_automation_target_registry", fail)
    assert readiness.main(("registry", "repository", "installation")) == 1
    assert capsys.readouterr().err.endswith("target-credentials-not-distinct.\n")


def test_interrupts_propagate(monkeypatch) -> None:
    def interrupt(*args, **kwargs):
        raise KeyboardInterrupt

    monkeypatch.setattr(selection, "load_automation_target_registry", interrupt)
    with pytest.raises(KeyboardInterrupt):
        selection.main(("registry", "desktop-runtime-host"))

    monkeypatch.setattr(readiness, "load_automation_target_registry", interrupt)
    with pytest.raises(KeyboardInterrupt):
        readiness.main(("registry", "repository", "installation"))
