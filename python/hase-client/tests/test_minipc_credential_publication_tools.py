from pathlib import Path
TOOLS=Path(__file__).parents[1]/"tools"
def src(n): return (TOOLS/n).read_text(encoding="utf-8")
def test_publication_consumes_locked_prepared_plan():
 s=src("Publish-HaseMiniPcPythonCredential.ps1");assert "transaction-plan.json" in s;assert "merge-base --is-ancestor" in s;assert "Get-FileHash" in s
def test_publication_composes_existing_reviewed_operator():
 s=src("Publish-HaseMiniPcPythonCredential.ps1");assert "Hase.Python.CredentialProvisioning.Operator" in s;assert '"provision"' in s;assert "--expected-authorization-policy-sha256" in s
def test_application_profile_is_committed_last():
 s=src("Publish-HaseMiniPcPythonCredential.ps1");assert s.index('status="credential-published"') < s.index("[IO.File]::WriteAllBytes($app") < s.index('status="committed"');assert "application-profile-commit" in s;assert "[IO.File]::Replace" not in s
def test_publication_never_starts_runtime_or_client():
 s=src("Publish-HaseMiniPcPythonCredential.ps1");assert "Start-Process" not in s;assert "Hase.DesktopHost.App,Hase.Client.Wpf.App" in s
def test_recovery_restores_sources_and_removes_only_new_targets():
 s=src("Recover-HaseMiniPcPythonCredentialPublication.ps1");assert "enrollment.original" in s;assert "application-profile.original" in s;assert "WriteAllBytes" in s;assert 'foreach($f in @($cert,$key,$profile,$policy))' in s;assert "Remove-HaseMiniPcPythonClientAuthority" not in s
def test_outer_journal_requires_explicit_recovery():
 s=src("Publish-HaseMiniPcPythonCredential.ps1");assert "publication-journal.json" in s;assert "recovery-required" in s;assert "explicit recovery may be required" in s
