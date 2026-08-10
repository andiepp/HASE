from pathlib import Path


TOOLS = Path(__file__).parents[1] / "tools"


def source(name: str) -> str:
    return (TOOLS / name).read_text(encoding="utf-8")


def test_preparation_is_minipc_machine_and_revision_locked() -> None:
    text = source("Initialize-HaseMiniPcLaptopPythonCredentialTransaction.ps1")
    assert 'COMPUTERNAME -cne "LABC"' in text
    assert "status --porcelain" in text
    assert "rev-parse origin/main" in text
    assert 'Get-Process -Name "Hase.DesktopHost.App"' in text


def test_preparation_reuses_strict_paired_readiness() -> None:
    text = source("Initialize-HaseMiniPcLaptopPythonCredentialTransaction.ps1")
    assert "Test-HaseMiniPcLaptopPythonCredentialReadiness.ps1" in text
    assert "AuthorityManifestPath" in text
    assert "AuthorityRollbackEvidencePath" in text


def test_preparation_does_not_issue_or_publish() -> None:
    text = source("Initialize-HaseMiniPcLaptopPythonCredentialTransaction.ps1")
    assert "New-SelfSignedCertificate" not in text
    assert "PythonClientCredential" not in text
    assert "Compress-Archive" not in text
    assert "Move-Item" not in text


def test_plan_uses_distinct_laptop_principal_and_two_grants() -> None:
    text = source("Initialize-HaseMiniPcLaptopPythonCredentialTransaction.ps1")
    assert '$laptopPrincipal = "hase-laptop-python-minipc"' in text
    assert '"runtime-host.snapshot.read"' in text
    assert '"property.authoritative.read"' in text
    for forbidden in ("property.write", "command.execute", "diagnostics.subscribe"):
        assert forbidden not in text


def test_plan_revision_locks_all_active_and_new_targets() -> None:
    text = source("Initialize-HaseMiniPcLaptopPythonCredentialTransaction.ps1")
    for name in (
        "stagingDirectory", "certificate", "privateKey", "pythonProfile",
        "transferArchive", "enrollment", "authorizationPolicy", "applicationProfile",
    ):
        assert f'name = "{name}"' in text
    assert 'purpose = "hase-minipc-laptop-python-credential-transaction"' in text


def test_preparation_secures_template_and_exact_originals() -> None:
    text = source("Initialize-HaseMiniPcLaptopPythonCredentialTransaction.ps1")
    assert "Set-PrivateDirectory" in text
    assert "Set-PrivateFile" in text
    assert '"enrollment.original"' in text
    assert '"authorization-policy.original"' in text
    assert '"application-profile.original"' in text


def test_recovery_refuses_changed_or_published_state() -> None:
    text = source("Restore-HaseMiniPcLaptopPythonCredentialPreparation.ps1")
    assert 'throw "publication-state"' in text
    assert "Get-FileHash" in text
    assert "profileTemplatePath" in text
    assert "Remove-Item -LiteralPath $rollback" in text
