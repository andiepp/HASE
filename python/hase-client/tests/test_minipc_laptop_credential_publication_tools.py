from pathlib import Path


ROOT = Path(__file__).parents[3]
TOOLS = Path(__file__).parents[1] / "tools"


def source(name: str) -> str:
    return (TOOLS / name).read_text(encoding="utf-8")


def test_publication_consumes_exact_locked_plan() -> None:
    text = source("Publish-HaseMiniPcLaptopPythonCredential.ps1")
    assert "transaction-plan.json" in text
    assert "merge-base --is-ancestor" in text
    assert 'purpose -cne "hase-minipc-laptop-python-credential-transaction"' in text
    assert "Get-FileHash" in text


def test_publication_uses_distinct_reviewed_operator_command() -> None:
    text = source("Publish-HaseMiniPcLaptopPythonCredential.ps1")
    assert "Hase.Python.CredentialProvisioning.Operator" in text
    assert '"provision-laptop-minipc"' in text
    assert "--expected-authorization-policy-sha256" in text


def test_operator_principal_is_fixed_and_existing_command_unchanged() -> None:
    text = (ROOT / "src" / "Hase.Python.CredentialProvisioning.Operator" /
            "PythonCredentialProvisioningOperator.cs").read_text(encoding="utf-8")
    assert 'case "provision-laptop-minipc"' in text
    assert 'args[1..], "hase-laptop-python-minipc"' in text
    assert 'args[1..], "hase-python-automation"' in text


def test_preparer_and_publisher_use_locked_plan_principal() -> None:
    preparer = (ROOT / "src" / "Hase.Python.CredentialProvisioning" /
                "PythonCredentialProvisioningPreparer.cs").read_text(encoding="utf-8")
    publisher = (ROOT / "src" / "Hase.Python.CredentialProvisioning" /
                 "PythonCredentialProvisioningPublisher.cs").read_text(encoding="utf-8")
    assert "LaptopMiniPcPrincipalId" in preparer
    assert 'writer.WriteString("principalId", plan.PrincipalId)' in preparer
    assert "!= plan.PrincipalId" in publisher


def test_transfer_profile_uses_explicit_laptop_paths() -> None:
    text = source("Publish-HaseMiniPcLaptopPythonCredential.ps1")
    for parameter in (
        "LaptopCertificatePath", "LaptopPrivateKeyPath", "LaptopProfilePath",
        "LaptopTrustedServerCertificatePath",
    ):
        assert parameter in text
    assert "transfer-manifest.json" in text
    assert "Compress-Archive" in text


def test_publication_has_outer_journal_and_explicit_recovery() -> None:
    text = source("Publish-HaseMiniPcLaptopPythonCredential.ps1")
    assert "publication-journal.json" in text
    assert 'status = "created"' in text
    assert '$phase = "credential-published"' in text
    assert '$phase = "committed"' in text
    for phase in (
        "custody-created", "profile-rewritten", "scope-validated",
        "manifest-written", "package-created",
    ):
        assert phase in text
    assert "failed at phase '$phase'" in text
    assert "explicit recovery may be required" in text


def test_transfer_archive_uses_exact_verified_input_list() -> None:
    text = source("Publish-HaseMiniPcLaptopPythonCredential.ps1")
    assert "$archiveInputs = @($certificate, $privateKey, $profile, $manifest)" in text
    assert "-LiteralPath $archiveInputs" in text
    assert "(Get-Item -LiteralPath $transfer).Length -le 0" in text


def test_staging_files_keep_inherited_protected_acl() -> None:
    text = source("Publish-HaseMiniPcLaptopPythonCredential.ps1")
    assert "(Get-Acl -LiteralPath $profile).AreAccessRulesProtected" in text
    assert "(Get-Acl -LiteralPath $manifest).AreAccessRulesProtected" in text
    assert "Set-PrivateFile $profile" not in text
    assert "Set-PrivateFile $manifest" not in text
    assert "Set-PrivateFile $transfer" in text


def test_recovery_restores_all_active_files_and_removes_new_outputs() -> None:
    text = source("Recover-HaseMiniPcLaptopPythonCredentialPublication.ps1")
    for original in (
        "enrollment.original", "authorization-policy.original",
        "application-profile.original",
    ):
        assert original in text
    assert "transfer-manifest.json" in text
    assert "Remove-Item -LiteralPath $transfer" in text
    assert "Preparation evidence retained : True" in text


def test_publication_never_starts_runtime_or_client() -> None:
    text = source("Publish-HaseMiniPcLaptopPythonCredential.ps1")
    assert "Start-Process" not in text
    assert 'Get-Process -Name "Hase.DesktopHost.App"' in text
