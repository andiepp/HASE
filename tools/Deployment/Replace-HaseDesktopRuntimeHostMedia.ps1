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
    [bool]$ExpectedCurrentAudioConfigured = $false,
    [bool]$ExpectedReplacementAudioConfigured = $false,
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
    -ExpectedCurrentAudioConfigured $ExpectedCurrentAudioConfigured `
    -ExpectedReplacementAudioConfigured $ExpectedReplacementAudioConfigured
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
$policyTemporary = $plan.PolicyPath + ".55f4." + `
    $plan.TransactionId + ".tmp"
$mediaReplacementBackup = Join-Path $transactionDirectory `
    "desktop-runtime-media.replace-backup.json"
$policyReplacementBackup = Join-Path $transactionDirectory `
    "runtime-host-authorization.replace-backup.json"
$temporaryPaths = @(
    $mediaTemporary,
    $policyTemporary,
    $mediaReplacementBackup,
    $policyReplacementBackup
)
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

$mediaAccessSddl = Get-HaseFileAccessSddl $plan.MediaPath
$policyAccessSddl = Get-HaseFileAccessSddl $plan.PolicyPath

$policyMutated = $false
$mediaMutated = $false
try {
Copy-Item -LiteralPath $plan.CandidatePath -Destination $mediaTemporary
if ((Get-HaseRequiredFileHash $mediaTemporary `
        "prepared replacement media") -cne $plan.CandidateHash) {
    throw "The prepared replacement media hash is not exact."
}
[void](Get-HaseMediaReplacementSources `
    -Path $mediaTemporary `
    -Role "prepared replacement media" `
    -ExpectedSourceCount $plan.ReplacementSourceCount `
    -ExpectedAudioConfigured $plan.ReplacementAudioConfigured)

$replacementPolicyHash = $plan.PolicyHash
if ($plan.PolicyChangeRequired) {
    Write-HaseUtf8Json $policyTemporary $plan.ReplacementPolicyDocument
    $replacementPolicyHash = Get-HaseRequiredFileHash $policyTemporary `
        "prepared replacement authorization policy"
    [void](Get-HaseMediaAuthorizationState `
        -Path $policyTemporary `
        -Role "prepared replacement authorization policy" `
        -ExpectedAudioConfigured $plan.ReplacementAudioConfigured `
        -ExpectedPrincipalId $plan.MediaPrincipalId)
}

$preparedManifest = [ordered]@{
    formatVersion = 2
    transactionId = $plan.TransactionId
    state = "prepared"
    originalProfileSha256 = $plan.ProfileHash
    originalPolicySha256 = $plan.PolicyHash
    replacementPolicySha256 = $replacementPolicyHash
    originalMediaSha256 = $plan.ActiveMediaHash
    replacementMediaSha256 = $plan.CandidateHash
    candidateSha256 = $plan.CandidateHash
    originalSourceCount = $plan.CurrentSourceCount
    replacementSourceCount = $plan.ReplacementSourceCount
    originalMediaGrantCount = $plan.CurrentMediaGrantCount
    replacementMediaGrantCount = $plan.ReplacementMediaGrantCount
    currentAudioConfigured = $plan.CurrentAudioConfigured
    replacementAudioConfigured = $plan.ReplacementAudioConfigured
    policyChanged = $plan.PolicyChangeRequired
}
if ($transactionDirectoryReused -and
    (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    $existingManifest = Read-HaseBoundedJson $manifestPath `
        "existing media replacement manifest"
    Assert-HaseExactProperties $existingManifest @(
        "formatVersion",
        "transactionId",
        "state",
        "originalProfileSha256",
        "originalPolicySha256",
        "replacementPolicySha256",
        "originalMediaSha256",
        "replacementMediaSha256",
        "candidateSha256",
        "originalSourceCount",
        "replacementSourceCount",
        "originalMediaGrantCount",
        "replacementMediaGrantCount",
        "currentAudioConfigured",
        "replacementAudioConfigured",
        "policyChanged"
    ) "existing media replacement manifest"
    $existingManifestExact =
        [int]$existingManifest.formatVersion -eq 2 -and
        [string]$existingManifest.transactionId -ceq
            $plan.TransactionId -and
        [string]$existingManifest.state -ceq "prepared" -and
        [string]$existingManifest.originalProfileSha256 -ceq
            $plan.ProfileHash -and
        [string]$existingManifest.originalPolicySha256 -ceq
            $plan.PolicyHash -and
        [string]$existingManifest.replacementPolicySha256 -ceq
            $replacementPolicyHash -and
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
        [int]$existingManifest.originalMediaGrantCount -eq
            $plan.CurrentMediaGrantCount -and
        [int]$existingManifest.replacementMediaGrantCount -eq
            $plan.ReplacementMediaGrantCount -and
        $existingManifest.currentAudioConfigured -is [bool] -and
        [bool]$existingManifest.currentAudioConfigured -eq
            $plan.CurrentAudioConfigured -and
        $existingManifest.replacementAudioConfigured -is [bool] -and
        [bool]$existingManifest.replacementAudioConfigured -eq
            $plan.ReplacementAudioConfigured -and
        $existingManifest.policyChanged -is [bool] -and
        [bool]$existingManifest.policyChanged -eq
            $plan.PolicyChangeRequired
    if (-not $existingManifestExact) {
        throw "The existing prepared replacement transaction is not exact."
    }
}
else {
    Write-HaseUtf8Json $manifestPath $preparedManifest
}

    if ($plan.PolicyChangeRequired) {
        [System.IO.File]::Replace(
            $policyTemporary,
            $plan.PolicyPath,
            $policyReplacementBackup,
            $true)
        $policyMutated = $true
        if ((Get-HaseRequiredFileHash $policyReplacementBackup `
                "authorization replacement backup") -cne
                $plan.PolicyHash) {
            throw "The authorization replacement backup is not byte-exact."
        }
        Set-HaseFileAccessSddl $plan.PolicyPath $policyAccessSddl
    }

    [System.IO.File]::Replace(
        $mediaTemporary,
        $plan.MediaPath,
        $mediaReplacementBackup,
        $true)
    $mediaMutated = $true
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
            "replaced authorization policy") -cne
            $replacementPolicyHash) {
        throw "The active-media replacement failed independent hash verification."
    }
    [void](Get-HaseMediaReplacementSources `
        -Path $plan.MediaPath `
        -Role "replaced media configuration" `
        -ExpectedSourceCount $plan.ReplacementSourceCount `
        -ExpectedAudioConfigured $plan.ReplacementAudioConfigured)
    [void](Get-HaseMediaAuthorizationState `
        -Path $plan.PolicyPath `
        -Role "replaced Runtime Host authorization policy" `
        -ExpectedAudioConfigured $plan.ReplacementAudioConfigured `
        -ExpectedPrincipalId $plan.MediaPrincipalId)

    $replacedManifest = [ordered]@{}
    foreach ($property in $preparedManifest.GetEnumerator()) {
        $replacedManifest[$property.Key] = $property.Value
    }
    $replacedManifest["state"] = "replaced"
    Write-HaseUtf8Json $manifestPath $replacedManifest
}
catch {
    $replacementFailure = $_
    if ($policyMutated -or $mediaMutated) {
        try {
            if ($mediaMutated) {
                Copy-Item -LiteralPath $mediaBackup `
                    -Destination $plan.MediaPath -Force
                Set-HaseFileAccessSddl $plan.MediaPath $mediaAccessSddl
            }
            if ($policyMutated) {
                Copy-Item -LiteralPath $policyBackup `
                    -Destination $plan.PolicyPath -Force
                Set-HaseFileAccessSddl $plan.PolicyPath $policyAccessSddl
            }
            if ((Get-HaseRequiredFileHash $plan.MediaPath `
                    "rolled-back media configuration") -cne
                    $plan.ActiveMediaHash -or
                (Get-HaseRequiredFileHash $plan.ProfilePath `
                    "preserved application profile") -cne
                    $plan.ProfileHash -or
                (Get-HaseRequiredFileHash $plan.PolicyPath `
                    "rolled-back authorization policy") -cne
                    $plan.PolicyHash) {
                throw "The active-media rollback hashes are not exact."
            }
            [void](Get-HaseMediaAuthorizationState `
                -Path $plan.PolicyPath `
                -Role "rolled-back Runtime Host authorization policy" `
                -ExpectedAudioConfigured $plan.CurrentAudioConfigured `
                -ExpectedPrincipalId $plan.MediaPrincipalId)
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
Write-Host "Computer exact               :" `
    ($env:COMPUTERNAME -ceq "AEPRAKETE")
Write-Host "Transaction ID               :" $plan.TransactionId
Write-Host "Recovery directory           :" $transactionDirectory
Write-Host "Recovery manifest SHA-256    :" $manifestHash
Write-Host "Original source count        :" $plan.CurrentSourceCount
Write-Host "Replacement source count     :" $plan.ReplacementSourceCount
Write-Host "Original media grant count   :" $plan.CurrentMediaGrantCount
Write-Host "Replacement media grant count:" $plan.ReplacementMediaGrantCount
Write-Host "Current microphone configured:" $plan.CurrentAudioConfigured
Write-Host "Replacement microphone configured:" `
    $plan.ReplacementAudioConfigured
Write-Host "Policy changed               :" $plan.PolicyChangeRequired
Write-Host "Profile hash preserved       :" $true
Write-Host "Sensitive values withheld    :" $true
Write-Host ""
Write-Host "No application was started and no device, capture, signaling,"
Write-Host "credential, serial, firmware, or physical output was accessed."
