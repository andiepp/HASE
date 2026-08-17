[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ExpectedRepositoryCommit,
    [Parameter(Mandatory = $true)]
    [string]$TransactionId,
    [Parameter(Mandatory = $true)]
    [string]$ExpectedManifestSha256,
    [string]$RepositoryPath = "H:\Development"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot "HaseMediaEnablement.Common.ps1")

if ($env:COMPUTERNAME -cne "AEPRAKETE") {
    throw "Run this tool only on AEPRAKETE."
}
if ($TransactionId -cnotmatch '^[0-9a-f]{64}$' -or
    $ExpectedManifestSha256 -cnotmatch '^[0-9A-Fa-f]{64}$') {
    throw "The recovery transaction inputs are invalid."
}
[void](Invoke-HaseGitLines $RepositoryPath @("fetch", "origin", "main"))
Assert-HaseRepositoryState $RepositoryPath $ExpectedRepositoryCommit
Assert-HaseApplicationsStopped

$installationRoot = Join-Path $env:LOCALAPPDATA "HASE\RuntimeHost"
$configurationRoot = Join-Path $installationRoot "Configuration"
$profilePath = Join-Path $configurationRoot "desktop-runtime-host.json"
$policyPath = Join-Path $configurationRoot `
    "runtime-host-authorization.json"
$mediaPath = Join-Path $configurationRoot "desktop-runtime-media.json"
$transactionDirectory = Join-Path $installationRoot `
    ("Recovery\ADR-0055-55F\" + $TransactionId)
$manifestPath = Join-Path $transactionDirectory "transaction.json"
$profileBackup = Join-Path $transactionDirectory `
    "desktop-runtime-host.before.json"
$policyBackup = Join-Path $transactionDirectory `
    "runtime-host-authorization.before.json"

$manifestHash = Get-HaseRequiredFileHash $manifestPath `
    "media enablement recovery manifest"
if ($manifestHash -cne $ExpectedManifestSha256.ToUpperInvariant()) {
    throw "The recovery manifest hash does not match."
}
$manifest = Read-HaseBoundedJson $manifestPath `
    "media enablement recovery manifest"
if ([int]$manifest.formatVersion -ne 1 -or
    [string]$manifest.transactionId -cne $TransactionId -or
    [string]$manifest.state -cne "enabled") {
    throw "The recovery manifest is not in the enabled state."
}
if ((Get-HaseRequiredFileHash $profilePath "enabled profile") -cne
        [string]$manifest.enabledProfileSha256 -or
    (Get-HaseRequiredFileHash $policyPath "enabled policy") -cne
        [string]$manifest.enabledPolicySha256 -or
    (Get-HaseRequiredFileHash $mediaPath "enabled media configuration") -cne
        [string]$manifest.enabledMediaSha256 -or
    (Get-HaseRequiredFileHash $profileBackup "profile recovery copy") -cne
        [string]$manifest.originalProfileSha256 -or
    (Get-HaseRequiredFileHash $policyBackup "policy recovery copy") -cne
        [string]$manifest.originalPolicySha256) {
    throw "Installed or recovery state changed after media enablement."
}

$profileAccessSddl = Get-HaseFileAccessSddl $profilePath
$policyAccessSddl = Get-HaseFileAccessSddl $policyPath
$profileTemporary = $profilePath + ".restore." + $TransactionId + ".tmp"
$policyTemporary = $policyPath + ".restore." + $TransactionId + ".tmp"
try {
    Copy-Item -LiteralPath $profileBackup -Destination $profileTemporary
    Copy-Item -LiteralPath $policyBackup -Destination $policyTemporary
    [System.IO.File]::Replace(
        $profileTemporary,
        $profilePath,
        $null,
        $true)
    Set-HaseFileAccessSddl $profilePath $profileAccessSddl
    [System.IO.File]::Replace(
        $policyTemporary,
        $policyPath,
        $null,
        $true)
    Set-HaseFileAccessSddl $policyPath $policyAccessSddl
    Remove-Item -LiteralPath $mediaPath -Force

    if ((Get-HaseRequiredFileHash $profilePath "restored profile") -cne
            [string]$manifest.originalProfileSha256 -or
        (Get-HaseRequiredFileHash $policyPath "restored policy") -cne
            [string]$manifest.originalPolicySha256 -or
        (Test-Path -LiteralPath $mediaPath)) {
        throw "The restored Runtime Host state failed independent verification."
    }
    $manifest.state = "restored"
    Write-HaseUtf8Json $manifestPath $manifest
}
finally {
    Remove-Item -LiteralPath $profileTemporary, $policyTemporary `
        -Force -ErrorAction SilentlyContinue
}

$restoredManifestHash = Get-HaseRequiredFileHash $manifestPath `
    "restored media enablement manifest"
Write-Host ""
Write-Host "ADR-0055 Runtime Host media enablement restored"
Write-Host ""
Write-Host "Computer exact            :" ($env:COMPUTERNAME -ceq "AEPRAKETE")
Write-Host "Transaction ID            :" $TransactionId
Write-Host "Recovery evidence retained:" $true
Write-Host "Restored manifest SHA-256 :" $restoredManifestHash
Write-Host "Media configuration absent:" (-not (Test-Path -LiteralPath $mediaPath))
Write-Host "Sensitive values withheld :" $true
Write-Host ""
Write-Host "No application was started and no device, capture, signaling,"
Write-Host "credential, serial, firmware, or physical output was accessed."
