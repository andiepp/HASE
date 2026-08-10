from pathlib import Path


TOOLS = Path(__file__).parents[1] / "tools"


def _source() -> str:
    return (TOOLS / "Test-HaseClientPythonAutomationReadiness.ps1").read_text(
        encoding="utf-8"
    )


def test_readiness_tool_uses_repository_local_private_environment() -> None:
    source = _source()
    assert '".venv\\Scripts\\python.exe"' in source
    assert "hase._client_automation_target_readiness" in source
    assert "Set-StrictMode -Version Latest" in source


def test_readiness_tool_requires_explicit_registry_and_installation() -> None:
    source = _source()
    assert "TargetRegistryPath" in source
    assert "InstallationDirectory" in source
    assert "$repositoryRoot" in source


def test_readiness_tool_contains_no_connection_or_mutation_operation() -> None:
    source = _source()
    for forbidden in (
        "open_runtime_host_channel",
        "WriteProperty",
        "ExecuteCommand",
        "Enable-Hase",
        "New-Hase",
    ):
        assert forbidden not in source
