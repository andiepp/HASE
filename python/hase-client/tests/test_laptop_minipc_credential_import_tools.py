from pathlib import Path


TOOLS = Path(__file__).parents[1] / "tools"


def source(name: str) -> str:
    return (TOOLS / name).read_text(encoding="utf-8")


def test_import_is_laptop_revision_locked_and_stopped() -> None:
    text = source("Import-HaseClientMiniPcPythonCredential.ps1")
    assert 'COMPUTERNAME -cne $ExpectedComputer' in text
    assert '[string] $ExpectedComputer' in text
    assert "status --porcelain" in text and "rev-parse origin/main" in text
    assert 'Get-Process -Name "Hase.DesktopHost.App"' in text


def test_import_validates_zip_without_expand_archive() -> None:
    text = source("Import-HaseClientMiniPcPythonCredential.ps1")
    assert "ZipFile]::OpenRead" in text
    assert "ComputeHash($stream)" in text
    assert "Expand-Archive" not in text
    for name in (
        "client-certificate.pem", "private-key.pem",
        "runtime-host-profile.json", "transfer-manifest.json",
    ):
        assert name in text


def test_import_requires_exact_manifest_destinations() -> None:
    text = source("Import-HaseClientMiniPcPythonCredential.ps1")
    assert 'purpose -cne "hase-laptop-python-minipc-credential-transfer"' in text
    assert 'principalId -cne "hase-laptop-python-minipc"' in text
    for field in (
        "certificatePath", "privateKeyPath", "profilePath",
        "trustedServerCertificatePath",
    ):
        assert f"manifest.destination.{field}" in text


def test_import_validates_pair_profiles_and_registry_without_connection() -> None:
    text = source("Import-HaseClientMiniPcPythonCredential.ps1")
    assert "load_cert_chain" in text
    assert "load_runtime_host_profile" in text
    assert "load_automation_target_registry" in text
    assert "server-certificates-not-distinct" in text
    assert "open_runtime_host_channel" not in text
    assert "RuntimeHostClient" not in text


def test_import_publishes_exact_two_target_registry() -> None:
    text = source("Import-HaseClientMiniPcPythonCredential.ps1")
    assert 'targetId="desktop-runtime-host"' in text
    assert 'targetId="minipc-runtime-host"' in text
    assert 'displayName="Desktop Runtime Host"' in text
    assert 'displayName="MiniPC Runtime Host"' in text


def test_import_preserves_desktop_and_removes_incoming_only_after_validation() -> None:
    text = source("Import-HaseClientMiniPcPythonCredential.ps1")
    assert "desktopProfileSha256" in text and "desktopProfileSddl" in text
    assert text.index("load_automation_target_registry") < text.index(
        "Remove-Item -LiteralPath $archivePath")
    assert "Incoming archive removed        : True" in text


def test_import_uses_private_staging_journal_and_rollback() -> None:
    text = source("Import-HaseClientMiniPcPythonCredential.ps1")
    assert "Set-PrivateDirectory" in text and "Set-PrivateFile" in text
    assert '"credential.stage"' in text
    assert 'Remove-Item -LiteralPath (Join-Path $stage "transfer-manifest.json")' in text
    assert '"import-journal.json"' in text
    assert "explicit recovery may be required" in text


def test_recovery_removes_only_new_laptop_targets() -> None:
    text = source("Recover-HaseClientMiniPcPythonCredentialImport.ps1")
    assert "desktopProfileSha256" in text and "desktopProfileSddl" in text
    assert "targetRegistryPath" in text
    assert "credentialDirectory" in text
    assert '"credential.stage"' in text
    assert "Rollback evidence retained   : True" in text
