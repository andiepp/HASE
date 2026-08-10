from pathlib import Path

import pytest


TOOLS = Path(__file__).parents[1] / "tools"


def _source(name: str) -> str:
    return (TOOLS / name).read_text(encoding="utf-8")


@pytest.mark.parametrize(
    "name",
    [
        "Test-HaseMiniPcLaptopPythonCredentialReadiness.ps1",
        "Test-HaseClientMiniPcPythonCredentialReadiness.ps1",
    ],
)
def test_tools_are_strictly_read_only(name: str) -> None:
    source = _source(name)
    for mutation in (
        "New-Item",
        "Set-Content",
        "WriteAllText",
        "Copy-Item",
        "Move-Item",
        "Remove-Item",
        "Set-Acl",
        "New-SelfSignedCertificate",
    ):
        assert mutation not in source


@pytest.mark.parametrize(
    "name",
    [
        "Test-HaseMiniPcLaptopPythonCredentialReadiness.ps1",
        "Test-HaseClientMiniPcPythonCredentialReadiness.ps1",
    ],
)
def test_tools_require_clean_repository_and_stopped_processes(name: str) -> None:
    source = _source(name)
    assert "status --porcelain" in source
    assert "rev-parse origin/main" in source
    assert 'Get-Process -Name "Hase.DesktopHost.App"' in source
    assert 'Get-Process -Name "Hase.Client.Wpf.App"' in source


def test_minipc_tool_requires_existing_dedicated_authority() -> None:
    source = _source("Test-HaseMiniPcLaptopPythonCredentialReadiness.ps1")
    assert "AuthorityManifestPath" in source
    assert "AuthorityRollbackEvidencePath" in source
    assert '"hase-minipc-python-client-authority"' in source
    assert '"CurrentUser/My"' in source
    assert '"CurrentUser/Root"' in source
    assert "HasPrivateKey" in source
    assert "certificateSha256" in source


def test_minipc_tool_uses_distinct_laptop_principal() -> None:
    source = _source("Test-HaseMiniPcLaptopPythonCredentialReadiness.ps1")
    assert '$laptopPrincipal = "hase-laptop-python-minipc"' in source
    assert "laptop-principal-present" in source
    assert 'principalId -eq "hase-python-automation"' in source


def test_minipc_tool_preserves_local_python_and_plans_two_grants() -> None:
    source = _source("Test-HaseMiniPcLaptopPythonCredentialReadiness.ps1")
    assert '"runtime-host.snapshot.read"' in source
    assert '"property.authoritative.read"' in source
    assert "$localGrants.Count -ne 2" in source
    assert "$plannedPermissions" in source
    assert "Two minimal grants planned      : True" in source
    planned = source[
        source.index("$plannedPermissions"):source.index("function Resolve-AbsolutePath")
    ]
    for forbidden in ("property.write", "command.execute", "diagnostics.subscribe"):
        assert forbidden not in planned


def test_minipc_tool_requires_existing_client_access() -> None:
    source = _source("Test-HaseMiniPcLaptopPythonCredentialReadiness.ps1")
    assert "$clientPrincipals.Count -lt 1" in source
    assert "$clientPermissions" in source
    assert "$permissions.Count -ne 6" in source
    assert "Existing Client access ready    : True" in source


def test_minipc_tool_requires_absent_external_transaction_targets() -> None:
    source = _source("Test-HaseMiniPcLaptopPythonCredentialReadiness.ps1")
    for parameter in (
        "StagingDirectory",
        "CertificatePath",
        "PrivateKeyPath",
        "ProfilePath",
        "TransferArchivePath",
        "RollbackDirectory",
    ):
        assert f"${parameter}" in source
    assert "staging-custody" in source
    assert "external-custody" in source


def test_laptop_tool_validates_existing_desktop_profile_without_connection() -> None:
    source = _source("Test-HaseClientMiniPcPythonCredentialReadiness.ps1")
    assert "DesktopProfilePath" in source
    assert "load_runtime_host_profile" in source
    assert "open_runtime_host_channel" not in source
    assert "RuntimeHostClient" not in source


def test_laptop_tool_requires_absent_distinct_external_custody() -> None:
    source = _source("Test-HaseClientMiniPcPythonCredentialReadiness.ps1")
    for parameter in (
        "AutomationInstallationDirectory",
        "MiniPcCredentialDirectory",
        "MiniPcCertificatePath",
        "MiniPcPrivateKeyPath",
        "MiniPcProfilePath",
        "TargetRegistryPath",
        "TransferArchivePath",
        "RollbackDirectory",
    ):
        assert f"${parameter}" in source
    assert "credential-custody" in source
    assert "profile-sharing" in source


def test_tools_print_only_fixed_readiness_outcomes() -> None:
    minipc = _source("Test-HaseMiniPcLaptopPythonCredentialReadiness.ps1")
    laptop = _source("Test-HaseClientMiniPcPythonCredentialReadiness.ps1")
    assert "MiniPC Laptop credential ready  : True" in minipc
    assert "Laptop MiniPC credential ready  : True" in laptop
    assert "Write-Host $" not in minipc
    assert "Write-Host $" not in laptop
