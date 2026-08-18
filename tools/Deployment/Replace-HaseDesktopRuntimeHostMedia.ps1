[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ExpectedRepositoryCommit,
    [Parameter(Mandatory = $true)]
    [string]$CandidatePath,
    [Parameter(Mandatory = $true)]
    [string]$CandidateSha256,
    [Parameter(Mandatory = $true)]
    [string]$ActiveMediaSha256,
    [Parameter(Mandatory = $true)]
    [string]$ExpectedTransactionId,
    [ValidateRange(1, 16)]
    [int]$ExpectedCurrentSourceCount = 1,
    [ValidateRange(1, 16)]
    [int]$ExpectedReplacementSourceCount = 2,
    [string]$RepositoryPath = "H:\Development"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot "HaseMediaReplacement.Common.ps1")

if ($env:COMPUTERNAME -cne "AEPRAKETE") {
    throw "Run this tool only on AEPRAKETE."
}
if ($ExpectedTransactionId -cnotmatch '^[0-9a-f]{64}$') {
    throw "The expected transaction identity is invalid."
}
[void](Invoke-HaseGitLines $RepositoryPath @("fetch", "origin", "main"))
Assert-HaseRepositoryState $RepositoryPath $ExpectedRepositoryCommit
$plan = Get-HaseMediaReplacementPlan `
    -CandidatePath $CandidatePath `
    -ExpectedCandidateHash $CandidateSha256 `
    -ExpectedActiveMediaHash $ActiveMediaSha256 `
    -ExpectedCurrentSourceCount $ExpectedCurrentSourceCount `
    -ExpectedReplacementSourceCount $ExpectedReplacementSourceCount `
    -ExpectedAudioConfigured $false
if ($plan.TransactionId -cne $ExpectedTransactionId) {
    throw "The active-media replacement transaction changed after preflight."
}

$recoveryRoot = Join-Path $plan.InstallationRoot `
    "Recovery\ADR-0055-55F4-Rebind"
$transactionDirectory = Join-Path $recoveryRoot $plan.TransactionId
$currentUserSid = (
    [System.Security.Principal.WindowsIdentity]::GetCurrent()).User
$profileBackup = Join-Path $transactionDirectory `
    "desktop-runtime-host.before.json"
$policyBackup = Join-Path $transactionDirectory `
    "runtime-host-authorization.before.json"
$mediaBackup = Join-Path $transactionDirectory `
    "desktop-runtime-media.before.json"
$manifestPath = Join-Path $transactionDirectory "transaction.json"
$mediaTemporary = $plan.MediaPath + ".55f4." + `
    $plan.TransactionId + ".tmp"
$mediaReplacementBackup = Join-Path $transactionDirectory `
    "desktop-runtime-media.replace-backup.json"
$temporaryPaths = @($mediaTemporary, $mediaReplacementBackup)
foreach ($temporaryPath in $temporaryPaths) {
    if (Test-Path -LiteralPath $temporaryPath) {
        throw "A replacement transaction temporary file already exists."
    }
}

$transactionDirectoryReused = Test-Path -LiteralPath $transactionDirectory
if ($transactionDirectoryReused) {
    if (-not (Test-Path -LiteralPath $transactionDirectory `
            -PathType Container)) {
        throw "The replacement recovery path is not a directory."
    }
}
else {
    [void](New-Item -ItemType Directory -Path $transactionDirectory -Force)
    Set-HaseProtectedDirectoryAccessControl `
        $transactionDirectory $currentUserSid
    Copy-Item -LiteralPath $plan.ProfilePath -Destination $profileBackup
    Copy-Item -LiteralPath $plan.PolicyPath -Destination $policyBackup
    Copy-Item -LiteralPath $plan.MediaPath -Destination $mediaBackup
}
if (-not (Test-HaseProtectedDirectoryAccessControl `
        $transactionDirectory $currentUserSid)) {
    throw "The replacement recovery directory permissions are not exact."
}
if ((Get-HaseRequiredFileHash $profileBackup "profile backup") -cne
        $plan.ProfileHash -or
    (Get-HaseRequiredFileHash $policyBackup "policy backup") -cne
        $plan.PolicyHash -or
    (Get-HaseRequiredFileHash $mediaBackup "media backup") -cne
        $plan.ActiveMediaHash) {
    throw "The replacement recovery copies are not byte-exact."
}

$preparedManifest = [ordered]@{
    formatVersion = 1
    transactionId = $plan.TransactionId
    state = "prepared"
    originalProfileSha256 = $plan.ProfileHash
    originalPolicySha256 = $plan.PolicyHash
    originalMediaSha256 = $plan.ActiveMediaHash
    replacementMediaSha256 = $plan.CandidateHash
    candidateSha256 = $plan.CandidateHash
    originalSourceCount = $plan.CurrentSourceCount
    replacementSourceCount = $plan.ReplacementSourceCount
    mediaGrantCount = $plan.MediaGrantCount
    audioConfigured = $plan.AudioConfigured
}
if ($transactionDirectoryReused) {
    $existingManifest = Read-HaseBoundedJson $manifestPath `
        "existing media replacement manifest"
    Assert-HaseExactProperties $existingManifest @(
        "formatVersion",
        "transactionId",
        "state",
        "originalProfileSha256",
        "originalPolicySha256",
        "originalMediaSha256",
        "replacementMediaSha256",
        "candidateSha256",
        "originalSourceCount",
        "replacementSourceCount",
        "mediaGrantCount",
        "audioConfigured"
    ) "existing media replacement manifest"
    $existingManifestExact =
        [int]$existingManifest.formatVersion -eq 1 -and
        [string]$existingManifest.transactionId -ceq
            $plan.TransactionId -and
        [string]$existingManifest.state -ceq "prepared" -and
        [string]$existingManifest.originalProfileSha256 -ceq
            $plan.ProfileHash -and
        [string]$existingManifest.originalPolicySha256 -ceq
            $plan.PolicyHash -and
        [string]$existingManifest.originalMediaSha256 -ceq
            $plan.ActiveMediaHash -and
        [string]$existingManifest.replacementMediaSha256 -ceq
            $plan.CandidateHash -and
        [string]$existingManifest.candidateSha256 -ceq
            $plan.CandidateHash -and
        [int]$existingManifest.originalSourceCount -eq
            $plan.CurrentSourceCount -and
        [int]$existingManifest.replacementSourceCount -eq
            $plan.ReplacementSourceCount -and
        [int]$existingManifest.mediaGrantCount -eq
            $plan.MediaGrantCount -and
        $existingManifest.audioConfigured -is [bool] -and
        [bool]$existingManifest.audioConfigured -eq
            $plan.AudioConfigured
    if (-not $existingManifestExact) {
        throw "The existing prepared replacement transaction is not exact."
    }
}
else {
    Write-HaseUtf8Json $manifestPath $preparedManifest
}

$mediaAccessSddl = Get-HaseFileAccessSddl $plan.MediaPath
$mutationStarted = $false
try {
    Copy-Item -LiteralPath $plan.CandidatePath `
        -Destination $mediaTemporary
    if ((Get-HaseRequiredFileHash $mediaTemporary `
            "prepared replacement media") -cne $plan.CandidateHash) {
        throw "The prepared replacement media hash is not exact."
    }
    [void](Get-HaseMediaReplacementSources `
        -Path $mediaTemporary `
        -Role "prepared replacement media" `
        -ExpectedSourceCount $plan.ReplacementSourceCount `
        -ExpectedAudioConfigured $plan.AudioConfigured)

    $mutationStarted = $true
    [System.IO.File]::Replace(
        $mediaTemporary,
        $plan.MediaPath,
        $mediaReplacementBackup,
        $true)
    if ((Get-HaseRequiredFileHash $mediaReplacementBackup `
            "media replacement backup") -cne $plan.ActiveMediaHash) {
        throw "The media replacement backup is not byte-exact."
    }
    Set-HaseFileAccessSddl $plan.MediaPath $mediaAccessSddl

    if ((Get-HaseRequiredFileHash $plan.MediaPath `
            "replaced media configuration") -cne $plan.CandidateHash -or
        (Get-HaseRequiredFileHash $plan.ProfilePath `
            "preserved application profile") -cne $plan.ProfileHash -or
        (Get-HaseRequiredFileHash $plan.PolicyPath `
            "preserved authorization policy") -cne $plan.PolicyHash) {
        throw "The active-media replacement failed independent hash verification."
    }
    [void](Get-HaseMediaReplacementSources `
        -Path $plan.MediaPath `
        -Role "replaced media configuration" `
        -ExpectedSourceCount $plan.ReplacementSourceCount `
        -ExpectedAudioConfigured $plan.AudioConfigured)

    Write-HaseUtf8Json $manifestPath ([ordered]@{
        formatVersion = 1
        transactionId = $plan.TransactionId
        state = "replaced"
        originalProfileSha256 = $plan.ProfileHash
        originalPolicySha256 = $plan.PolicyHash
        originalMediaSha256 = $plan.ActiveMediaHash
        replacementMediaSha256 = $plan.CandidateHash
        candidateSha256 = $plan.CandidateHash
        originalSourceCount = $plan.CurrentSourceCount
        replacementSourceCount = $plan.ReplacementSourceCount
        mediaGrantCount = $plan.MediaGrantCount
        audioConfigured = $plan.AudioConfigured
    })
}
catch {
    $replacementFailure = $_
    if ($mutationStarted) {
        try {
            Copy-Item -LiteralPath $mediaBackup `
                -Destination $plan.MediaPath -Force
            Set-HaseFileAccessSddl $plan.MediaPath $mediaAccessSddl
            if ((Get-HaseRequiredFileHash $plan.MediaPath `
                    "rolled-back media configuration") -cne
                    $plan.ActiveMediaHash -or
                (Get-HaseRequiredFileHash $plan.ProfilePath `
                    "preserved application profile") -cne
                    $plan.ProfileHash -or
                (Get-HaseRequiredFileHash $plan.PolicyPath `
                    "preserved authorization policy") -cne
                    $plan.PolicyHash) {
                throw "The active-media rollback hashes are not exact."
            }
            Write-HaseUtf8Json $manifestPath $preparedManifest
        }
        catch {
            throw "Active-media replacement and rollback both failed: $($_.Exception.Message)"
        }
    }
    throw $replacementFailure
}
finally {
    foreach ($temporaryPath in $temporaryPaths) {
        Remove-Item -LiteralPath $temporaryPath -Force `
            -ErrorAction SilentlyContinue
    }
}

$manifestHash = Get-HaseRequiredFileHash $manifestPath `
    "media replacement recovery manifest"
Write-Host ""
Write-Host "ADR-0055 Runtime Host active-media replacement succeeded"
Write-Host ""
Write-Host "Computer exact             :" ($env:COMPUTERNAME -ceq "AEPRAKETE")
Write-Host "Transaction ID             :" $plan.TransactionId
Write-Host "Recovery directory         :" $transactionDirectory
Write-Host "Recovery manifest SHA-256  :" $manifestHash
Write-Host "Original source count      :" $plan.CurrentSourceCount
Write-Host "Replacement source count   :" $plan.ReplacementSourceCount
Write-Host "Media grant count preserved:" $plan.MediaGrantCount
Write-Host "Profile hash preserved     :" $true
Write-Host "Policy hash preserved      :" $true
Write-Host "Microphone configured      :" $plan.AudioConfigured
Write-Host "Sensitive values withheld  :" $true
Write-Host ""
Write-Host "No application was started and no device, capture, signaling,"
Write-Host "authorization, credential, serial, firmware, or physical output"
Write-Host "was accessed."
