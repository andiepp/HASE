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
if (Test-Path -LiteralPath $transactionDirectory) {
    throw "Recovery evidence already exists for this transaction."
}
[void](New-Item -ItemType Directory -Path $transactionDirectory -Force)
$currentUserSid = (
    [System.Security.Principal.WindowsIdentity]::GetCurrent()).User
Set-HaseProtectedDirectoryAccessControl `
    $transactionDirectory $currentUserSid
if (-not (Test-HaseProtectedDirectoryAccessControl `
        $transactionDirectory $currentUserSid)) {
    throw "The recovery directory permissions are not exact."
}

$profileBackup = Join-Path $transactionDirectory `
    "desktop-runtime-host.before.json"
$policyBackup = Join-Path $transactionDirectory `
    "runtime-host-authorization.before.json"
$manifestPath = Join-Path $transactionDirectory "transaction.json"
Copy-Item -LiteralPath $plan.ProfilePath -Destination $profileBackup
Copy-Item -LiteralPath $plan.PolicyPath -Destination $policyBackup
if ((Get-HaseRequiredFileHash $profileBackup "profile backup") -cne
        $plan.ProfileHash -or
    (Get-HaseRequiredFileHash $policyBackup "policy backup") -cne
        $plan.PolicyHash) {
    throw "The recovery copies are not byte-exact."
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
$temporaryPaths = @($profileTemporary, $policyTemporary, $mediaTemporary)
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
        @($preparedMedia.sources).Count -ne 1) {
        throw "The prepared media enablement documents failed validation."
    }

    Write-HaseUtf8Json $manifestPath ([ordered]@{
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
    })

    $mutationStarted = $true
    [System.IO.File]::Move($mediaTemporary, $plan.MediaPath)
    Set-HaseFileAccessSddl $plan.MediaPath $profileAccessSddl
    [System.IO.File]::Replace(
        $policyTemporary,
        $plan.PolicyPath,
        $null,
        $true)
    Set-HaseFileAccessSddl $plan.PolicyPath $policyAccessSddl
    [System.IO.File]::Replace(
        $profileTemporary,
        $plan.ProfilePath,
        $null,
        $true)
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
