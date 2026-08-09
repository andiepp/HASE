from pathlib import Path

TOOLS=Path(__file__).parents[1]/"tools"
def src(name): return (TOOLS/name).read_text(encoding="utf-8")

def test_preparation_is_not_publication():
 s=src("Initialize-HaseMiniPcPythonProvisioningTransaction.ps1")
 assert "New-SelfSignedCertificate" not in s
 assert "PythonClientCredential" not in s
 assert "authorization-policy.candidate.json" in s
 assert "application-profile.candidate.json" in s

def test_prepares_six_file_security_transaction_and_directory():
 s=src("Initialize-HaseMiniPcPythonProvisioningTransaction.ps1")
 for name in ("provisioningDirectory","certificate","privateKey","pythonProfile","enrollment","authorizationPolicy","applicationProfile"): assert f'name="{name}"' in s

def test_preserves_existing_client_effective_permissions():
 s=src("Initialize-HaseMiniPcPythonProvisioningTransaction.ps1")
 for permission in ("runtime-host.snapshot.read","property.cached.read","property.authoritative.read","property.write","command.execute","observation.subscribe"): assert permission in s
 assert "diagnostics.subscribe" not in s

def test_python_starts_with_minimal_read_only_grants():
 s=src("Initialize-HaseMiniPcPythonProvisioningTransaction.ps1")
 assert 'pythonGrants=@("runtime-host.snapshot.read","property.authoritative.read")' in s

def test_requires_authority_clean_repo_and_stopped_processes():
 s=src("Initialize-HaseMiniPcPythonProvisioningTransaction.ps1")
 assert "status --porcelain" in s and "origin/main" in s
 assert "Hase.DesktopHost.App,Hase.Client.Wpf.App" in s
 assert "AuthorityRollbackEvidencePath" in s

def test_recovery_refuses_changed_or_partially_published_state():
 s=src("Restore-HaseMiniPcPythonProvisioningPreparation.ps1")
 assert 'throw"publication-state"' in s
 assert "Get-FileHash" in s
 assert "Remove-Item $template" in s
