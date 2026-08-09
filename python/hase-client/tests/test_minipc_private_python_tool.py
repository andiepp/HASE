from pathlib import Path


TOOLS = Path(__file__).parents[1] / "tools"
INSTALLER = TOOLS / "Install-HaseMiniPcPrivatePython.ps1"
READINESS = TOOLS / "Test-HasePythonCredentialProvisioningReadiness.ps1"


def _installer() -> str:
    return INSTALLER.read_text(encoding="utf-8")


def test_pins_official_runtime_identity_and_hash() -> None:
    source = _installer()
    assert "3.13.1|64bit" in source
    assert "9877d0d24f7978407bde1b50ab1023b0f5c67ff6c9816b834e5258db1a636249" in source
    assert "Get-FileHash" in source


def test_uses_archive_without_system_installer_or_registration() -> None:
    source = _installer()
    assert "Expand-Archive" in source
    assert "msiexec" not in source.lower()
    assert "Start-Process" not in source
    assert "SetEnvironmentVariable" not in source
    assert "Set-ItemProperty" not in source
    assert "New-ItemProperty" not in source


def test_requires_clean_synchronized_repository() -> None:
    source = _installer()
    assert "status --porcelain" in source
    assert "rev-parse HEAD" in source
    assert "rev-parse origin/main" in source


def test_creates_only_private_runtime_and_local_environment() -> None:
    source = _installer()
    assert 'Join-Path $env:LOCALAPPDATA "HASE"' in source
    assert 'Join-Path $packageDirectory ".venv"' in source
    assert "-m venv" in source
    assert "requirements-development.txt" in source
    assert "--editable $packageDirectory" in source


def test_rolls_back_only_self_created_targets() -> None:
    source = _installer()
    assert "$environmentCreated" in source
    assert "$runtimePublished" in source
    assert "$stagePath" in source
    assert "Remove-Item -LiteralPath $environmentPath" in source
    assert "Remove-Item -LiteralPath $runtimePath" in source


def test_readiness_classifies_broken_interpreter_as_environment_failure() -> None:
    source = READINESS.read_text(encoding="utf-8")
    environment_check = source.index("sys.version_info[:2]")
    configuration_classification = source.index(
        '$failureClassification = "ConfigurationInvalid"'
    )
    assert environment_check < configuration_classification
