<#
.SYNOPSIS
Verifies a re-issued Python automation credential (ADR-0083), read-only.

.DESCRIPTION
With the re-issue journal present it requires the committed phase, every
target holding its candidate and every retained old file its original
bytes, the authorization policy unchanged, the expired credential gone
from the enrollment registry and the replacement enrolled for the
principal. After the retained files were removed it requires the
installed credential to be valid and enrolled for the principal. It
prints credential identifiers and dates only.
#>
[CmdletBinding()]
param(
    [string] $RuntimeHostConfigurationPath = (Join-Path $env:LOCALAPPDATA `
        "HASE\RuntimeHost\Configuration\desktop-runtime-host.json"),
    [string] $PythonProfilePath = (Join-Path $env:LOCALAPPDATA `
        "HASE\PythonAutomation\Security\runtime-host-profile.json"),
    [string] $PrincipalId = "hase-python-automation"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Get-HaseFileSha256([string] $Path)
{
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return "" }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-HaseShortId([string] $CredentialId)
{
    if ($CredentialId.Length -le 24) { return $CredentialId }
    return $CredentialId.Substring(0, 24) + "..."
}

try
{
    $hostDocument = Get-Content -LiteralPath $RuntimeHostConfigurationPath -Raw |
        ConvertFrom-Json
    $policyPath = [IO.Path]::GetFullPath([string]$hostDocument.authorizationPolicyFilePath)
    $network = Get-Content -LiteralPath `
        ([string]$hostDocument.privateNetworkConfigurationFilePath) -Raw | ConvertFrom-Json
    $enrollmentPath = [IO.Path]::GetFullPath([string]$network.clientEnrollmentFilePath)
    $profilePath = [IO.Path]::GetFullPath($PythonProfilePath)
    $profileDocument = Get-Content -LiteralPath $profilePath -Raw | ConvertFrom-Json
    $certificatePath = [IO.Path]::GetFullPath(
        [string]$profileDocument.clientCertificate.certificateChainPath)

    $certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new(
        $certificatePath)
    $sha = [Security.Cryptography.SHA256]::Create()
    try
    {
        $credentialId = "x509-sha256:" + [BitConverter]::ToString(
            $sha.ComputeHash($certificate.RawData)).Replace("-", "").ToLowerInvariant()
        $notAfter = $certificate.NotAfter.ToUniversalTime()
        $notBefore = $certificate.NotBefore.ToUniversalTime()
    }
    finally
    {
        $sha.Dispose()
        $certificate.Dispose()
    }
    $now = [DateTime]::UtcNow
    if ($now -lt $notBefore -or $now -ge $notAfter) { throw "validity" }

    $registry = Get-Content -LiteralPath $enrollmentPath -Raw | ConvertFrom-Json
    $installed = @($registry.enrollments | Where-Object {
        [string]$_.credentialId -ceq $credentialId
    })
    if ($installed.Count -ne 1 -or [string]$installed[0].principalId -cne $PrincipalId)
    {
        throw "enrollment"
    }

    $journalPath = Join-Path (Split-Path -Parent $profilePath) `
        "python-credential-reissue.transaction.json"
    $journalState = "absent"
    if (Test-Path -LiteralPath $journalPath -PathType Leaf)
    {
        $journal = Get-Content -LiteralPath $journalPath -Raw | ConvertFrom-Json
        if ([string]$journal.Phase -cne "committed") { throw "phase" }
        if ([string]$journal.ReplacementCredentialId -cne $credentialId) { throw "replacement" }
        $expired = @($registry.enrollments | Where-Object {
            [string]$_.credentialId -ceq [string]$journal.ExpiredCredentialId
        })
        if ($expired.Count -ne 0) { throw "expired-enrolled" }
        if ((Get-HaseFileSha256 $policyPath) -cne [string]$journal.AuthorizationPolicySha256)
        {
            throw "policy"
        }
        $entries = @($journal.Entries)
        if ($entries.Count -ne 4) { throw "journal" }
        foreach ($entry in $entries)
        {
            if ((Get-HaseFileSha256 ([string]$entry.TargetPath)) -cne
                    [string]$entry.CandidateSha256 -or
                (Get-HaseFileSha256 ([string]$entry.BackupPath)) -cne
                    [string]$entry.OriginalSha256)
            {
                throw "files"
            }
        }
        $journalState = "committed, old files retained"
        Write-Host ("Expired credential removed  : {0}" -f `
            (Get-HaseShortId ([string]$journal.ExpiredCredentialId)))
    }

    Write-Host ("Installed credential        : {0}" -f (Get-HaseShortId $credentialId))
    Write-Host ("Valid until (UTC)           : {0:yyyy-MM-dd HH:mm}" -f $notAfter)
    Write-Host ("Enrolled for the principal  : True")
    Write-Host ("Re-issue journal            : {0}" -f $journalState)
    Write-Host "Re-issue verified           : True"
    exit 0
}
catch
{
    Write-Error "Python credential re-issue verification failed."
    exit 1
}
