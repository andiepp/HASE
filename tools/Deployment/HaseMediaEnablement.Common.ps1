$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$script:HaseMediaPermissions = @(
    "media.capability.read",
    "media.video.receive",
    "media.session.start",
    "media.session.negotiate",
    "media.session.stop"
)

function Get-HaseRequiredFileHash {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Role
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "The $Role is missing."
    }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Read-HaseBoundedJson {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Role,
        [int]$MaximumBytes = 65536
    )

    if (-not [System.IO.Path]::IsPathRooted($Path) -or
        $Path -match '^[A-Za-z]:[^\\/]') {
        throw "The $Role path must be fully qualified."
    }
    $file = Get-Item -LiteralPath $Path -ErrorAction Stop
    if ($file.Length -lt 1 -or $file.Length -gt $MaximumBytes) {
        throw "The $Role has an invalid byte length."
    }
    try {
        return Get-Content -LiteralPath $Path -Raw -Encoding UTF8 |
            ConvertFrom-Json
    }
    catch {
        throw "The $Role is not valid JSON."
    }
}

function Test-HaseHasProperty {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Document,
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    return @($Document.PSObject.Properties.Name) -contains $Name
}

function Assert-HaseExactProperties {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Document,
        [Parameter(Mandatory = $true)]
        [string[]]$Names,
        [Parameter(Mandatory = $true)]
        [string]$Role
    )

    $actual = @($Document.PSObject.Properties.Name | Sort-Object)
    $expected = @($Names | Sort-Object)
    if ($actual.Count -ne $expected.Count) {
        throw "The $Role has an unexpected structure."
    }
    for ($index = 0; $index -lt $expected.Count; $index++) {
        if ($actual[$index] -cne $expected[$index]) {
            throw "The $Role has an unexpected structure."
        }
    }
}

function Assert-HaseApplicationsStopped {
    $hostCount = @(Get-Process -Name "Hase.DesktopHost.App" `
        -ErrorAction SilentlyContinue).Count
    $clientCount = @(Get-Process -Name "Hase.Client.Wpf.App" `
        -ErrorAction SilentlyContinue).Count
    if ($hostCount -ne 0 -or $clientCount -ne 0) {
        throw "Close all HASE Runtime Host and Client applications first."
    }
}

function Invoke-HaseGitLines {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryPath,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $stdoutPath = [System.IO.Path]::GetTempFileName()
    $stderrPath = [System.IO.Path]::GetTempFileName()
    try {
        $process = Start-Process -FilePath "git.exe" `
            -ArgumentList (@("-C", $RepositoryPath) + $Arguments) `
            -NoNewWindow -Wait -PassThru `
            -RedirectStandardOutput $stdoutPath `
            -RedirectStandardError $stderrPath
        if ($process.ExitCode -ne 0) {
            $failure = [System.IO.File]::ReadAllText($stderrPath).Trim()
            throw "git $($Arguments -join ' ') failed: $failure"
        }
        return @([System.IO.File]::ReadAllLines($stdoutPath))
    }
    finally {
        Remove-Item -LiteralPath $stdoutPath, $stderrPath `
            -Force -ErrorAction SilentlyContinue
    }
}

function Assert-HaseRepositoryState {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryPath,
        [Parameter(Mandatory = $true)]
        [string]$ExpectedCommit
    )

    if (-not (Test-Path -LiteralPath $RepositoryPath -PathType Container)) {
        throw "The repository directory is missing."
    }
    $head = @(Invoke-HaseGitLines $RepositoryPath @("rev-parse", "HEAD"))
    $origin = @(Invoke-HaseGitLines $RepositoryPath `
        @("rev-parse", "origin/main"))
    $branch = @(Invoke-HaseGitLines $RepositoryPath `
        @("branch", "--show-current"))
    $status = @(Invoke-HaseGitLines $RepositoryPath `
        @("status", "--porcelain=v1"))
    if ($head.Count -ne 1 -or $head[0] -cne $ExpectedCommit -or
        $origin.Count -ne 1 -or $origin[0] -cne $ExpectedCommit -or
        $branch.Count -ne 1 -or $branch[0] -cne "main" -or
        $status.Count -ne 0) {
        throw "The repository is not clean and synchronized at the expected commit."
    }
}

function Get-HaseMediaEnablementPlan {
    param(
        [Parameter(Mandatory = $true)]
        [string]$CandidatePath,
        [Parameter(Mandatory = $true)]
        [string]$AuthorizationRequestPath,
        [Parameter(Mandatory = $true)]
        [string]$ExpectedCandidateHash,
        [Parameter(Mandatory = $true)]
        [string]$ExpectedAuthorizationRequestHash
    )

    Assert-HaseApplicationsStopped
    foreach ($artifactPath in @($CandidatePath, $AuthorizationRequestPath)) {
        if (-not [System.IO.Path]::IsPathRooted($artifactPath) -or
            $artifactPath -match '^[A-Za-z]:[^\\/]') {
            throw "Every media preparation artifact path must be fully qualified."
        }
    }
    $installationRoot = Join-Path $env:LOCALAPPDATA "HASE\RuntimeHost"
    $applicationRoot = Join-Path $installationRoot "Application"
    $configurationRoot = Join-Path $installationRoot "Configuration"
    $profilePath = Join-Path $configurationRoot "desktop-runtime-host.json"
    $mediaPath = Join-Path $configurationRoot "desktop-runtime-media.json"
    $executablePath = Join-Path $applicationRoot "Hase.DesktopHost.App.exe"
    foreach ($required in @(
        $executablePath,
        (Join-Path $applicationRoot "Microsoft.Web.WebView2.Core.dll"),
        (Join-Path $applicationRoot "Microsoft.Web.WebView2.Wpf.dll"))) {
        if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
            throw "The updated Runtime Host application is not ready."
        }
    }
    if (@(Get-ChildItem -LiteralPath $applicationRoot `
            -Filter "WebView2Loader.dll" -Recurse -File `
            -ErrorAction SilentlyContinue).Count -lt 1) {
        throw "The updated Runtime Host WebView2 loader is missing."
    }
    if (Test-Path -LiteralPath $mediaPath) {
        throw "Runtime Host media is already configured."
    }

    $candidateHash = Get-HaseRequiredFileHash $CandidatePath `
        "media binding candidate"
    $requestHash = Get-HaseRequiredFileHash $AuthorizationRequestPath `
        "Client media authorization request"
    if ($candidateHash -cne $ExpectedCandidateHash.ToUpperInvariant() -or
        $requestHash -cne $ExpectedAuthorizationRequestHash.ToUpperInvariant()) {
        throw "A preparation artifact hash does not match."
    }

    $profile = Read-HaseBoundedJson $profilePath `
        "Runtime Host application profile"
    if ([int]$profile.formatVersion -ne 1 -or
        (Test-HaseHasProperty $profile "mediaConfigurationFilePath") -or
        -not (Test-HaseHasProperty $profile "authorizationPolicyFilePath")) {
        throw "The Runtime Host application profile is not in the expected pre-media state."
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

    $identityPath = [System.IO.Path]::GetFullPath(
        [string]$profile.identityFilePath)
    $identity = Read-HaseBoundedJson $identityPath `
        "Runtime Host identity" 8192
    Assert-HaseExactProperties $identity `
        @("formatVersion", "runtimeHostId") "Runtime Host identity"
    if ([int]$identity.formatVersion -ne 1 -or
        [string]::IsNullOrWhiteSpace([string]$identity.runtimeHostId)) {
        throw "The Runtime Host identity is invalid."
    }

    $privateNetworkPath = [System.IO.Path]::GetFullPath(
        [string]$profile.privateNetworkConfigurationFilePath)
    $privateNetwork = Read-HaseBoundedJson $privateNetworkPath `
        "Runtime Host private-network configuration"
    if ([int]$privateNetwork.formatVersion -ne 1 -or
        -not (Test-HaseHasProperty $privateNetwork `
            "clientEnrollmentFilePath")) {
        throw "The Runtime Host private-network configuration is incomplete."
    }
    $enrollmentPath = [System.IO.Path]::GetFullPath(
        [string]$privateNetwork.clientEnrollmentFilePath)
    $enrollment = Read-HaseBoundedJson $enrollmentPath `
        "Runtime Host enrollment registry"
    $enrollments = @($enrollment.enrollments)
    if ([int]$enrollment.formatVersion -ne 1 -or
        $enrollments.Count -lt 1) {
        throw "The Runtime Host enrollment registry is incomplete."
    }

    $request = Read-HaseBoundedJson $AuthorizationRequestPath `
        "Client media authorization request"
    Assert-HaseExactProperties $request `
        @("formatVersion", "profiles") "Client media authorization request"
    $requestProfiles = @($request.profiles)
    if ([int]$request.formatVersion -ne 1 -or
        $requestProfiles.Count -lt 1 -or $requestProfiles.Count -gt 16) {
        throw "The Client media authorization request is invalid."
    }
    $hostMatches = @($requestProfiles | Where-Object {
        [string]$_.expectedRuntimeHostId -ceq [string]$identity.runtimeHostId
    })
    if ($hostMatches.Count -ne 1) {
        throw "The Client request does not select exactly one local Runtime Host profile."
    }
    Assert-HaseExactProperties $hostMatches[0] `
        @("expectedRuntimeHostId", "credentialId") `
        "Client media authorization profile"
    $credentialId = [string]$hostMatches[0].credentialId
    if ($credentialId -cnotmatch '^x509-sha256:[0-9a-f]{64}$') {
        throw "The Client request contains an invalid credential identity."
    }
    $enrollmentMatches = @($enrollments | Where-Object {
        [string]$_.credentialId -ceq $credentialId
    })
    if ($enrollmentMatches.Count -ne 1 -or
        [string]::IsNullOrWhiteSpace(
            [string]$enrollmentMatches[0].principalId)) {
        throw "The Client credential does not resolve to exactly one enrolled principal."
    }
    $principalId = [string]$enrollmentMatches[0].principalId

    $candidate = Read-HaseBoundedJson $CandidatePath `
        "media binding candidate"
    Assert-HaseExactProperties $candidate `
        @("formatVersion", "sources") "media binding candidate"
    $sources = @($candidate.sources)
    if ([int]$candidate.formatVersion -ne 1 -or $sources.Count -ne 1) {
        throw "The media binding candidate must contain exactly one source."
    }
    Assert-HaseExactProperties $sources[0] @(
        "mediaSourceId",
        "mediaSourceGeneration",
        "displayName",
        "videoDeviceId",
        "audioDeviceId"
    ) "media binding source"
    if ([string]::IsNullOrWhiteSpace([string]$sources[0].mediaSourceId) -or
        [string]::IsNullOrWhiteSpace(
            [string]$sources[0].mediaSourceGeneration) -or
        [string]::IsNullOrWhiteSpace([string]$sources[0].displayName) -or
        [string]::IsNullOrWhiteSpace([string]$sources[0].videoDeviceId)) {
        throw "The media binding source is incomplete."
    }

    $policy = Read-HaseBoundedJson $policyPath `
        "Runtime Host authorization policy"
    Assert-HaseExactProperties $policy @("formatVersion", "grants") `
        "Runtime Host authorization policy"
    $grants = @($policy.grants)
    if ([int]$policy.formatVersion -ne 1) {
        throw "The Runtime Host authorization-policy version is unsupported."
    }
    $existingMedia = @($grants | Where-Object {
        [string]$_.permission -like "media.*"
    })
    if ($existingMedia.Count -ne 0) {
        throw "The Runtime Host authorization policy already contains media grants."
    }
    if (@($grants | Where-Object {
            [string]$_.principalId -ceq $principalId
        }).Count -lt 1) {
        throw "The selected principal has no existing Runtime Host authorization."
    }

    $permissions = @($script:HaseMediaPermissions)
    $audioConfigured = -not [string]::IsNullOrWhiteSpace(
        [string]$sources[0].audioDeviceId)
    if ($audioConfigured) {
        $permissions += "media.audio.receive"
    }

    $profileHash = Get-HaseRequiredFileHash $profilePath `
        "Runtime Host application profile"
    $policyHash = Get-HaseRequiredFileHash $policyPath `
        "Runtime Host authorization policy"
    $identityHash = Get-HaseRequiredFileHash $identityPath `
        "Runtime Host identity"
    $privateNetworkHash = Get-HaseRequiredFileHash $privateNetworkPath `
        "Runtime Host private-network configuration"
    $enrollmentHash = Get-HaseRequiredFileHash $enrollmentPath `
        "Runtime Host enrollment registry"
    $transactionMaterial = @(
        $candidateHash,
        $requestHash,
        $profileHash,
        $policyHash,
        $identityHash,
        $privateNetworkHash,
        $enrollmentHash,
        $principalId,
        ($permissions -join ",")
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
        ConfigurationRoot = $configurationRoot
        ProfilePath = $profilePath
        MediaPath = $mediaPath
        PolicyPath = $policyPath
        IdentityPath = $identityPath
        PrivateNetworkPath = $privateNetworkPath
        EnrollmentPath = $enrollmentPath
        CandidatePath = [System.IO.Path]::GetFullPath($CandidatePath)
        AuthorizationRequestPath = [System.IO.Path]::GetFullPath(
            $AuthorizationRequestPath)
        Profile = $profile
        Policy = $policy
        PrincipalId = $principalId
        Permissions = $permissions
        AudioConfigured = $audioConfigured
        CandidateHash = $candidateHash
        AuthorizationRequestHash = $requestHash
        ProfileHash = $profileHash
        PolicyHash = $policyHash
        IdentityHash = $identityHash
        PrivateNetworkHash = $privateNetworkHash
        EnrollmentHash = $enrollmentHash
    }
}

function Write-HaseUtf8Json {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [object]$Document
    )

    $json = $Document | ConvertTo-Json -Depth 12
    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $json + "`r`n", $encoding)
}
