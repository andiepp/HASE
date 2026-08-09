from pathlib import Path


TOOLS = Path(__file__).parents[1] / "tools"


def _source(name: str) -> str:
    return (TOOLS / name).read_text(encoding="utf-8")


def test_installer_is_windows_powershell_compatible() -> None:
    source = _source("Install-HasePythonAutomation.ps1")
    assert "IsPathFullyQualified" not in source
    assert "utf8NoBOM" not in source
    assert "System.Text.UTF8Encoding" in source


def test_installer_verifies_hash_before_creating_target() -> None:
    source = _source("Install-HasePythonAutomation.ps1")
    assert source.index("package-hash-mismatch") < source.index(
        "CreateDirectory($InstallationDirectory)"
    )


def test_installer_rejects_existing_target_and_editable_install() -> None:
    source = _source("Install-HasePythonAutomation.ps1")
    assert "(Test-Path -LiteralPath $InstallationDirectory)" in source
    assert "--editable" not in source
    assert "--quiet" in source
    assert '$failureCode = "unexpected-failure"' in source
    assert "installation-target-inside-repository" in source


def test_installer_writes_only_non_sensitive_manifest_fields() -> None:
    source = _source("Install-HasePythonAutomation.ps1")
    for field in (
        "schemaVersion",
        "packageName",
        "packageVersion",
        "packageSha256",
        "pythonVersion",
        "installedAtUtc",
    ):
        assert field in source
    manifest = source[source.index("$manifest ="):source.index("$manifestJson")]
    assert "ProfilePath" not in manifest
    assert "PackagePath" not in manifest
    assert "InstallationDirectory" not in manifest


def test_launcher_uses_only_installed_environment() -> None:
    source = _source("Invoke-HasePythonAutomation.ps1")
    assert '$PSScriptRoot ".venv\\Scripts\\python.exe"' in source
    assert "$env:PYTHONPATH = $null" in source
    assert "hase._automation_health" in source
    assert "hase._automation_same_value_property_write" in source


def test_launcher_rejects_invalid_external_profile() -> None:
    source = _source("Invoke-HasePythonAutomation.ps1")
    assert "profile-path-invalid" in source
    assert "Test-AbsolutePath -Path $ProfilePath" in source
    assert "HASE automation failed: unexpected-failure." in source


def test_launcher_requires_explicit_confirmation_only_for_write() -> None:
    source = _source("Invoke-HasePythonAutomation.ps1")
    assert '[ValidateSet("Health", "Kel103SameValuePropertyWrite")]' in source
    assert "same-value-write-confirmation-required" in source
    assert "confirmation-not-applicable" in source
    assert '"confirm-same-value-write"' in source
    assert source.index("same-value-write-confirmation-required") < source.index(
        "profile-path-invalid"
    )
