from pathlib import Path


TOOLS = Path(__file__).parents[1] / "tools"


def _source(name: str) -> str:
    return (TOOLS / name).read_text(encoding="utf-8")


def test_creation_uses_dedicated_non_exportable_ca() -> None:
    source = _source("New-HaseMiniPcPythonClientAuthority.ps1")
    assert "HASE MiniPC Python Client Authority" in source
    assert "KeyExportPolicy NonExportable" in source
    assert "KeyLength 3072" in source
    assert "CertSign" in source
    assert "ca=true&pathlength=0" in source


def test_creation_does_not_touch_server_or_enrollment_state() -> None:
    source = _source("New-HaseMiniPcPythonClientAuthority.ps1")
    for forbidden in ("desktop-private-network", "client-enrollments", "authorizationPolicy"):
        assert forbidden not in source
    assert "status --porcelain" in source
    assert "rev-parse origin/main" in source


def test_creation_records_fixed_manifest_and_rollback_evidence() -> None:
    source = _source("New-HaseMiniPcPythonClientAuthority.ps1")
    assert "hase-minipc-python-client-authority" in source
    assert "certificateSha256" in source
    assert "CurrentUser/My" in source
    assert "CurrentUser/Root" in source
    assert "HashData" not in source
    assert "ToHexString" not in source


def test_removal_requires_matching_evidence_and_certificate() -> None:
    source = _source("Remove-HaseMiniPcPythonClientAuthority.ps1")
    assert "$manifest.thumbprint -cne $rollback.thumbprint" in source
    assert "$manifest.certificateSha256 -cne $rollback.certificateSha256" in source
    assert "$personal.Count -ne 1 -or $trusted.Count -ne 1" in source
    assert "certificateSha256" in source


def test_readiness_accepts_explicit_signing_root() -> None:
    general = _source("Test-HasePythonCredentialProvisioningReadiness.ps1")
    minipc = _source("Test-HaseMiniPcPythonProvisioningReadiness.ps1")
    assert "$SigningRootThumbprint" in general
    assert "SigningRootThumbprint $SigningRootThumbprint" in minipc
