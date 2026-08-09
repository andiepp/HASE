from pathlib import Path


TOOL = (
    Path(__file__).parents[1]
    / "tools"
    / "Test-HaseMiniPcPythonProvisioningReadiness.ps1"
)


def _source() -> str:
    return TOOL.read_text(encoding="utf-8")


def test_tool_is_read_only() -> None:
    source = _source()
    for mutation in (
        "New-Item",
        "Set-Content",
        "WriteAllText",
        "Copy-Item",
        "Move-Item",
        "Remove-Item",
        "Set-Acl",
    ):
        assert mutation not in source


def test_tool_is_windows_powershell_compatible() -> None:
    source = _source()
    assert "IsPathFullyQualified" not in source
    assert "utf8NoBOM" not in source
    assert "WindowsPowerShell\\v1.0\\powershell.exe" in source


def test_tool_requires_independent_external_outputs() -> None:
    source = _source()
    for parameter in (
        "ProfileTemplatePath",
        "CertificatePath",
        "PrivateKeyPath",
        "ProfilePath",
        "RollbackDirectory",
    ):
        assert f"$${parameter}" not in source
        assert f"$${parameter}".replace("$$", "$") in source
    assert "paths-not-distinct" in source
    assert "output-outside-provisioning" in source
    assert "template-inside-provisioning" in source
    assert "$provisioningParent = Split-Path -Parent $provisioningRoot" in source
    assert "(Test-Path -LiteralPath $provisioningRoot)" in source
    assert "New-Item" not in source


def test_tool_rejects_existing_python_identity_and_grants() -> None:
    source = _source()
    assert "$ApplicationProfilePath" in source
    assert "$AuthorizationPolicyPath" not in source
    assert '"privateNetworkConfigurationFilePath"' in source
    assert '"authorizationPolicyFilePath"' in source
    assert 'principalId -eq "hase-python-automation"' in source
    assert "python-identity-present" in source
    assert "python-grants-present" in source


def test_tool_requires_clean_repository_and_stopped_processes() -> None:
    source = _source()
    assert "git -C $repositoryRoot status --porcelain" in source
    assert "git -C $repositoryRoot rev-parse origin/main" in source
    assert 'Get-Process -Name "Hase.DesktopHost.App"' in source
    assert 'Get-Process -Name "Hase.Client.Wpf.App"' in source


def test_tool_matches_public_and_active_server_certificates() -> None:
    source = _source()
    assert "Get-PfxCertificate -FilePath $trustedCertificatePath" in source
    assert "$public.HasPrivateKey" in source
    assert "ToBase64String($active[0].RawData)" in source


def test_tool_rejects_retained_transaction_artifacts() -> None:
    source = _source()
    assert "$journals = @()" in source
    assert '$target + ".stage-"' in source
    assert '$target + ".backup-"' in source


def test_tool_prints_only_fixed_boolean_outcomes() -> None:
    source = _source()
    assert "MiniPC Python provisioning ready: True" in source
    assert "Write-Host $" not in source
    assert "Write-Output" not in source
