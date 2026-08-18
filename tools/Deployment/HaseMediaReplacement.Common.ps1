$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot "HaseMediaEnablement.Common.ps1")

function Get-HaseMediaReplacementSources {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Role,
        [Parameter(Mandatory = $true)]
        [int]$ExpectedSourceCount,
        [Parameter(Mandatory = $true)]
        [bool]$ExpectedAudioConfigured
    )

    $document = Read-HaseBoundedJson $Path $Role
    Assert-HaseExactProperties $document @("formatVersion", "sources") $Role
    $sources = @($document.sources)
    if ([int]$document.formatVersion -ne 1 -or
        $sources.Count -ne $ExpectedSourceCount -or
        $sources.Count -lt 1 -or $sources.Count -gt 16) {
        throw "The $Role source count is not exact."
    }

    foreach ($source in $sources) {
        Assert-HaseExactProperties $source @(
            "mediaSourceId",
            "mediaSourceGeneration",
            "displayName",
            "videoDeviceId",
            "audioDeviceId"
        ) "$Role source"
        if ([string]::IsNullOrWhiteSpace([string]$source.mediaSourceId) -or
            [string]::IsNullOrWhiteSpace(
                [string]$source.mediaSourceGeneration) -or
            [string]::IsNullOrWhiteSpace([string]$source.displayName) -or
            [string]::IsNullOrWhiteSpace([string]$source.videoDeviceId)) {
            throw "A $Role source is incomplete."
        }
    }

    $sourceIds = @($sources | ForEach-Object {
        [string]$_.mediaSourceId
    })
    $videoDeviceIds = @($sources | ForEach-Object {
        [string]$_.videoDeviceId
    })
    if (@($sourceIds | Sort-Object -Unique).Count -ne $sources.Count -or
        @($videoDeviceIds | Sort-Object -Unique).Count -ne $sources.Count) {
        throw "The $Role source and video-device identities must be unique."
    }

    $audioConfigured = @($sources | Where-Object {
        -not [string]::IsNullOrWhiteSpace([string]$_.audioDeviceId)
    }).Count -gt 0
    if ($audioConfigured -ne $ExpectedAudioConfigured) {
        throw "The $Role microphone state is not exact."
    }

    return $sources
}

function Get-HaseMediaReplacementPlan {
    param(
        [Parameter(Mandatory = $true)]
        [string]$CandidatePath,
        [Parameter(Mandatory = $true)]
        [string]$ExpectedCandidateHash,
        [Parameter(Mandatory = $true)]
        [string]$ExpectedActiveMediaHash,
        [int]$ExpectedCurrentSourceCount = 1,
        [int]$ExpectedReplacementSourceCount = 2,
        [bool]$ExpectedAudioConfigured = $false
    )

    Assert-HaseApplicationsStopped
    foreach ($hash in @($ExpectedCandidateHash, $ExpectedActiveMediaHash)) {
        if ($hash -cnotmatch '^[0-9A-Fa-f]{64}$') {
            throw "Every expected SHA-256 must contain exactly sixty-four hexadecimal characters."
        }
    }
    if ($ExpectedCurrentSourceCount -lt 1 -or
        $ExpectedCurrentSourceCount -gt 16 -or
        $ExpectedReplacementSourceCount -lt 1 -or
        $ExpectedReplacementSourceCount -gt 16) {
        throw "Every expected source count must be between one and sixteen."
    }

    $installationRoot = Join-Path $env:LOCALAPPDATA "HASE\RuntimeHost"
    $configurationRoot = Join-Path $installationRoot "Configuration"
    $preparationRoot = Join-Path $installationRoot "Preparation"
    $profilePath = Join-Path $configurationRoot "desktop-runtime-host.json"
    $expectedMediaPath = Join-Path $configurationRoot `
        "desktop-runtime-media.json"
    $expectedCandidatePath = Join-Path $preparationRoot `
        "desktop-runtime-media.candidate.json"
    $candidateFullPath = [System.IO.Path]::GetFullPath($CandidatePath)
    if (-not [string]::Equals(
            $candidateFullPath,
            $expectedCandidatePath,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "The replacement candidate is outside guided preparation custody."
    }

    $profile = Read-HaseBoundedJson $profilePath `
        "Runtime Host application profile"
    if ([int]$profile.formatVersion -ne 1 -or
        -not (Test-HaseHasProperty $profile "mediaConfigurationFilePath") -or
        -not (Test-HaseHasProperty $profile "authorizationPolicyFilePath")) {
        throw "The Runtime Host application profile is not media-enabled."
    }
    $mediaPath = [System.IO.Path]::GetFullPath(
        [string]$profile.mediaConfigurationFilePath)
    if (-not [string]::Equals(
            $mediaPath,
            $expectedMediaPath,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "The active media configuration is outside guided custody."
    }
    $policyPath = [System.IO.Path]::GetFullPath(
        [string]$profile.authorizationPolicyFilePath)
    $expectedPolicyPath = Join-Path $configurationRoot `
        "runtime-host-authorization.json"
    if (-not [string]::Equals(
            $policyPath,
            $expectedPolicyPath,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "The authorization policy is outside guided custody."
    }

    $candidateHash = Get-HaseRequiredFileHash $candidateFullPath `
        "replacement media candidate"
    $activeMediaHash = Get-HaseRequiredFileHash $mediaPath `
        "active media configuration"
    if ($candidateHash -cne $ExpectedCandidateHash.ToUpperInvariant() -or
        $activeMediaHash -cne
            $ExpectedActiveMediaHash.ToUpperInvariant()) {
        throw "A media replacement artifact hash does not match."
    }

    $activeSources = @(Get-HaseMediaReplacementSources `
        -Path $mediaPath `
        -Role "active media configuration" `
        -ExpectedSourceCount $ExpectedCurrentSourceCount `
        -ExpectedAudioConfigured $ExpectedAudioConfigured)
    $replacementSources = @(Get-HaseMediaReplacementSources `
        -Path $candidateFullPath `
        -Role "replacement media candidate" `
        -ExpectedSourceCount $ExpectedReplacementSourceCount `
        -ExpectedAudioConfigured $ExpectedAudioConfigured)

    $policy = Read-HaseBoundedJson $policyPath `
        "Runtime Host authorization policy"
    Assert-HaseExactProperties $policy @("formatVersion", "grants") `
        "Runtime Host authorization policy"
    $grants = @($policy.grants)
    $mediaGrants = @($grants | Where-Object {
        [string]$_.permission -like "media.*"
    })
    $actualPermissions = @($mediaGrants | ForEach-Object {
        [string]$_.permission
    } | Sort-Object -Unique)
    $expectedPermissions = @($script:HaseMediaPermissions | Sort-Object)
    $permissionDifference = @(Compare-Object `
        $expectedPermissions $actualPermissions)
    $mediaPrincipals = @($mediaGrants | ForEach-Object {
        [string]$_.principalId
    } | Sort-Object -Unique)
    if ([int]$policy.formatVersion -ne 1 -or
        $mediaGrants.Count -ne $script:HaseMediaPermissions.Count -or
        $permissionDifference.Count -ne 0 -or
        $mediaPrincipals.Count -ne 1 -or
        [string]::IsNullOrWhiteSpace($mediaPrincipals[0])) {
        throw "The existing video-only media authorization is not exact."
    }

    $profileHash = Get-HaseRequiredFileHash $profilePath `
        "Runtime Host application profile"
    $policyHash = Get-HaseRequiredFileHash $policyPath `
        "Runtime Host authorization policy"
    $transactionMaterial = @(
        $activeMediaHash,
        $candidateHash,
        $profileHash,
        $policyHash,
        $mediaPrincipals[0],
        $ExpectedCurrentSourceCount,
        $ExpectedReplacementSourceCount,
        $ExpectedAudioConfigured
    ) -join "`n"
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $transactionBytes = [System.Text.Encoding]::UTF8.GetBytes(
            $transactionMaterial)
        $transactionId = [System.BitConverter]::ToString(
            $sha.ComputeHash($transactionBytes)).Replace("-", "").ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }

    return [pscustomobject]@{
        TransactionId = $transactionId
        InstallationRoot = $installationRoot
        ProfilePath = $profilePath
        PolicyPath = $policyPath
        MediaPath = $mediaPath
        CandidatePath = $candidateFullPath
        ProfileHash = $profileHash
        PolicyHash = $policyHash
        ActiveMediaHash = $activeMediaHash
        CandidateHash = $candidateHash
        CurrentSourceCount = $activeSources.Count
        ReplacementSourceCount = $replacementSources.Count
        AudioConfigured = $ExpectedAudioConfigured
        MediaGrantCount = $mediaGrants.Count
    }
}
