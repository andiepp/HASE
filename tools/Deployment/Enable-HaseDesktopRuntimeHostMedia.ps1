[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ExpectedRepositoryCommit,
    [Parameter(Mandatory = $true)]
    [string]$CandidatePath,
    [Parameter(Mandatory = $true)]
    [string]$CandidateSha256,
    [Parameter(Mandatory = $true)]
    [string]$AuthorizationRequestPath,
    [Parameter(Mandatory = $true)]
    [string]$AuthorizationRequestSha256,
    [Parameter(Mandatory = $true)]
    [string]$ExpectedTransactionId,
    [string]$RepositoryPath = "H:\Development"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot "HaseMediaEnablement.Common.ps1")

if ($env:COMPUTERNAME -cne "AEPRAKETE") {
    throw "Run this tool only on AEPRAKETE."
}
if ($ExpectedTransactionId -cnotmatch '^[0-9a-f]{64}$') {
    throw "The expected transaction identity is invalid."
}
foreach ($hash in @($CandidateSha256, $AuthorizationRequestSha256)) {
    if ($hash -cnotmatch '^[0-9A-Fa-f]{64}$') {
        throw "Every expected SHA-256 must contain exactly sixty-four hexadecimal characters."
    }
}
[void](Invoke-HaseGitLines $RepositoryPath @("fetch", "origin", "main"))
Assert-HaseRepositoryState $RepositoryPath $ExpectedRepositoryCommit
$plan = Get-HaseMediaEnablementPlan `
    -CandidatePath $CandidatePath `
    -AuthorizationRequestPath $AuthorizationRequestPath `
    -ExpectedCandidateHash $CandidateSha256 `
    -ExpectedAuthorizationRequestHash $AuthorizationRequestSha256
if ($plan.TransactionId -cne $ExpectedTransactionId) {
    throw "The media enablement transaction identity changed after preflight."
}

$recoveryRoot = Join-Path $plan.InstallationRoot `
    "Recovery\ADR-0055-55F"
$transactionDirectory = Join-Path $recoveryRoot $plan.TransactionId
$currentUserSid = (
    [System.Security.Principal.WindowsIdentity]::GetCurrent()).User
$profileBackup = Join-Path $transactionDirectory `
    "desktop-runtime-host.before.json"
$policyBackup = Join-Path $transactionDirectory `
    "runtime-host-authorization.before.json"
$manifestPath = Join-Path $transactionDirectory "transaction.json"
$transactionDirectoryReused = Test-Path -LiteralPath $transactionDirectory
if ($transactionDirectoryReused) {
    if (-not (Test-Path -LiteralPath $transactionDirectory `
            -PathType Container)) {
        throw "The recovery transaction path is not a directory."
    }
}
else {
    [void](New-Item -ItemType Directory -Path $transactionDirectory -Force)
    Set-HaseProtectedDirectoryAccessControl `
        $transactionDirectory $currentUserSid
    Copy-Item -LiteralPath $plan.ProfilePath -Destination $profileBackup
    Copy-Item -LiteralPath $plan.PolicyPath -Destination $policyBackup
}
if (-not (Test-HaseProtectedDirectoryAccessControl `
        $transactionDirectory $currentUserSid)) {
    throw "The recovery directory permissions are not exact."
}
if ((Get-HaseRequiredFileHash $profileBackup "profile backup") -cne
        $plan.ProfileHash -or
    (Get-HaseRequiredFileHash $policyBackup "policy backup") -cne
        $plan.PolicyHash) {
    throw "The recovery copies are not byte-exact."
}
if ($transactionDirectoryReused -and
    -not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "The existing recovery transaction manifest is missing."
}

$profileDocument = [ordered]@{}
foreach ($property in $plan.Profile.PSObject.Properties) {
    $profileDocument[$property.Name] = $property.Value
}
$profileDocument["mediaConfigurationFilePath"] = $plan.MediaPath

$grantDocuments = New-Object System.Collections.Generic.List[object]
foreach ($grant in @($plan.Policy.grants)) {
    $grantDocuments.Add([ordered]@{
        principalId = [string]$grant.principalId
        permission = [string]$grant.permission
    })
}
foreach ($permission in $plan.Permissions) {
    $grantDocuments.Add([ordered]@{
        principalId = $plan.PrincipalId
        permission = $permission
    })
}
$policyDocument = [ordered]@{
    formatVersion = 1
    grants = $grantDocuments.ToArray()
}

$profileTemporary = $plan.ProfilePath + ".55f2." + `
    $plan.TransactionId + ".tmp"
$policyTemporary = $plan.PolicyPath + ".55f2." + `
    $plan.TransactionId + ".tmp"
$mediaTemporary = $plan.MediaPath + ".55f2." + `
    $plan.TransactionId + ".tmp"
$profileReplacementBackup = Join-Path $transactionDirectory `
    "desktop-runtime-host.replace-backup.json"
$policyReplacementBackup = Join-Path $transactionDirectory `
    "runtime-host-authorization.replace-backup.json"
$temporaryPaths = @(
    $profileTemporary,
    $policyTemporary,
    $mediaTemporary,
    $profileReplacementBackup,
    $policyReplacementBackup
)
foreach ($temporaryPath in $temporaryPaths) {
    if (Test-Path -LiteralPath $temporaryPath) {
        throw "A transaction temporary file already exists."
    }
}

$profileAccessSddl = Get-HaseFileAccessSddl $plan.ProfilePath
$policyAccessSddl = Get-HaseFileAccessSddl $plan.PolicyPath
$mutationStarted = $false
try {
    Write-HaseUtf8Json $profileTemporary $profileDocument
    Write-HaseUtf8Json $policyTemporary $policyDocument
    Copy-Item -LiteralPath $plan.CandidatePath -Destination $mediaTemporary
    $enabledProfileHash = Get-HaseRequiredFileHash $profileTemporary `
        "prepared application profile"
    $enabledPolicyHash = Get-HaseRequiredFileHash $policyTemporary `
        "prepared authorization policy"
    $enabledMediaHash = Get-HaseRequiredFileHash $mediaTemporary `
        "prepared media configuration"

    $preparedProfile = Read-HaseBoundedJson $profileTemporary `
        "prepared Runtime Host application profile"
    $preparedPolicy = Read-HaseBoundedJson $policyTemporary `
        "prepared Runtime Host authorization policy"
    $preparedMedia = Read-HaseBoundedJson $mediaTemporary `
        "prepared Runtime Host media configuration"
    if ([string]$preparedProfile.mediaConfigurationFilePath -cne
            $plan.MediaPath -or
        @($preparedPolicy.grants).Count -ne
            (@($plan.Policy.grants).Count + $plan.Permissions.Count) -or
        @($preparedMedia.sources).Count -ne $plan.SourceCount) {
        throw "The prepared media enablement documents failed validation."
    }

    $preparedManifestDocument = [ordered]@{
        formatVersion = 1
        transactionId = $plan.TransactionId
        state = "prepared"
        originalProfileSha256 = $plan.ProfileHash
        originalPolicySha256 = $plan.PolicyHash
        enabledProfileSha256 = $enabledProfileHash
        enabledPolicySha256 = $enabledPolicyHash
        enabledMediaSha256 = $enabledMediaHash
        candidateSha256 = $plan.CandidateHash
        authorizationRequestSha256 = $plan.AuthorizationRequestHash
        mediaGrantCount = $plan.Permissions.Count
        audioConfigured = $plan.AudioConfigured
    }
    if ($transactionDirectoryReused) {
        $existingManifest = Read-HaseBoundedJson $manifestPath `
            "existing media enablement recovery manifest"
        Assert-HaseExactProperties $existingManifest @(
            "formatVersion",
            "transactionId",
            "state",
            "originalProfileSha256",
            "originalPolicySha256",
            "enabledProfileSha256",
            "enabledPolicySha256",
            "enabledMediaSha256",
            "candidateSha256",
            "authorizationRequestSha256",
            "mediaGrantCount",
            "audioConfigured"
        ) "existing media enablement recovery manifest"
        $existingManifestMatches =
            [int]$existingManifest.formatVersion -eq 1 -and
            [string]$existingManifest.transactionId -ceq
                $plan.TransactionId -and
            [string]$existingManifest.state -ceq "prepared" -and
            [string]$existingManifest.originalProfileSha256 -ceq
                $plan.ProfileHash -and
            [string]$existingManifest.originalPolicySha256 -ceq
                $plan.PolicyHash -and
            [string]$existingManifest.enabledProfileSha256 -ceq
                $enabledProfileHash -and
            [string]$existingManifest.enabledPolicySha256 -ceq
                $enabledPolicyHash -and
            [string]$existingManifest.enabledMediaSha256 -ceq
                $enabledMediaHash -and
            [string]$existingManifest.candidateSha256 -ceq
                $plan.CandidateHash -and
            [string]$existingManifest.authorizationRequestSha256 -ceq
                $plan.AuthorizationRequestHash -and
            [int]$existingManifest.mediaGrantCount -eq
                $plan.Permissions.Count -and
            $existingManifest.audioConfigured -is [bool] -and
            [bool]$existingManifest.audioConfigured -eq
                $plan.AudioConfigured
        if (-not $existingManifestMatches) {
            throw "The existing prepared recovery transaction does not match the current plan."
        }
    }
    else {
        Write-HaseUtf8Json $manifestPath $preparedManifestDocument
    }

    $mutationStarted = $true
    [System.IO.File]::Move($mediaTemporary, $plan.MediaPath)
    Set-HaseFileAccessSddl $plan.MediaPath $profileAccessSddl
    [System.IO.File]::Replace(
        $policyTemporary,
        $plan.PolicyPath,
        $policyReplacementBackup,
        $true)
    if ((Get-HaseRequiredFileHash $policyReplacementBackup `
            "authorization replacement backup") -cne $plan.PolicyHash) {
        throw "The authorization replacement backup is not byte-exact."
    }
    Set-HaseFileAccessSddl $plan.PolicyPath $policyAccessSddl
    [System.IO.File]::Replace(
        $profileTemporary,
        $plan.ProfilePath,
        $profileReplacementBackup,
        $true)
    if ((Get-HaseRequiredFileHash $profileReplacementBackup `
            "profile replacement backup") -cne $plan.ProfileHash) {
        throw "The profile replacement backup is not byte-exact."
    }
    Set-HaseFileAccessSddl $plan.ProfilePath $profileAccessSddl

    if ((Get-HaseRequiredFileHash $plan.ProfilePath "enabled profile") -cne
            $enabledProfileHash -or
        (Get-HaseRequiredFileHash $plan.PolicyPath "enabled policy") -cne
            $enabledPolicyHash -or
        (Get-HaseRequiredFileHash $plan.MediaPath "enabled media") -cne
            $enabledMediaHash) {
        throw "The enabled Runtime Host media files failed independent hash verification."
    }

    Write-HaseUtf8Json $manifestPath ([ordered]@{
        formatVersion = 1
        transactionId = $plan.TransactionId
        state = "enabled"
        originalProfileSha256 = $plan.ProfileHash
        originalPolicySha256 = $plan.PolicyHash
        enabledProfileSha256 = $enabledProfileHash
        enabledPolicySha256 = $enabledPolicyHash
        enabledMediaSha256 = $enabledMediaHash
        candidateSha256 = $plan.CandidateHash
        authorizationRequestSha256 = $plan.AuthorizationRequestHash
        mediaGrantCount = $plan.Permissions.Count
        audioConfigured = $plan.AudioConfigured
    })
}
catch {
    if ($mutationStarted) {
        Copy-Item -LiteralPath $profileBackup `
            -Destination $plan.ProfilePath -Force
        Set-HaseFileAccessSddl $plan.ProfilePath $profileAccessSddl
        Copy-Item -LiteralPath $policyBackup `
            -Destination $plan.PolicyPath -Force
        Set-HaseFileAccessSddl $plan.PolicyPath $policyAccessSddl
        if (Test-Path -LiteralPath $plan.MediaPath -PathType Leaf) {
            Remove-Item -LiteralPath $plan.MediaPath -Force
        }
    }
    throw
}
finally {
    foreach ($temporaryPath in $temporaryPaths) {
        Remove-Item -LiteralPath $temporaryPath -Force `
            -ErrorAction SilentlyContinue
    }
}

$manifestHash = Get-HaseRequiredFileHash $manifestPath `
    "media enablement recovery manifest"
Write-Host ""
Write-Host "ADR-0055 Runtime Host media enablement succeeded"
Write-Host ""
Write-Host "Computer exact             :" ($env:COMPUTERNAME -ceq "AEPRAKETE")
Write-Host "Transaction ID             :" $plan.TransactionId
Write-Host "Recovery directory         :" $transactionDirectory
Write-Host "Recovery manifest SHA-256  :" $manifestHash
Write-Host "Media grant count added    :" $plan.Permissions.Count
Write-Host "Microphone configured      :" $plan.AudioConfigured
Write-Host "Sensitive values withheld  :" $true
Write-Host ""
Write-Host "No application was started and no device, capture, signaling,"
Write-Host "credential, serial, firmware, or physical output was accessed."
