from pathlib import Path
TOOLS=Path(__file__).parents[1]/"tools"
def source(name): return (TOOLS/name).read_text(encoding="utf-8")
def test_repair_is_laptop_and_revision_locked():
 s=source("Repair-HaseClientDesktopPythonProfileCustody.ps1");assert 'COMPUTERNAME -cne "LTAEP"' in s;assert "status --porcelain" in s;assert "rev-parse origin/main" in s
def test_repair_requires_exact_stale_custody_and_local_pair():
 s=source("Repair-HaseClientDesktopPythonProfileCustody.ps1");assert 'C:\\Users\\aeppi\\AppData\\Local\\HASE\\PythonAutomation\\Security\\python-client-chain.pem' in s;assert "load_cert_chain" in s;assert "server-certificates" in s
def test_repair_records_exact_rollback_and_verifies_profile():
 s=source("Repair-HaseClientDesktopPythonProfileCustody.ps1");assert "originalBase64" in s;assert "originalSha256" in s;assert "originalSddl" in s;assert "[IO.File]::Replace" in s;assert "load_runtime_host_profile" in s
def test_restore_requires_matching_evidence():
 s=source("Restore-HaseClientDesktopPythonProfileCustody.ps1");assert "originalSha256" in s;assert "originalSddl" in s;assert "WriteAllBytes" in s;assert "Set-Acl" in s
