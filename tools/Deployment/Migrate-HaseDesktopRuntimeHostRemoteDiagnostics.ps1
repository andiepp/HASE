[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$AuthorizationPolicyPath,
    [Parameter(Mandatory = $true)]
    [ValidateSet("Operational", "Protocol", "Bytes")]
    [string]$RemoteDiagnosticsMaximumLevel
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Get-FullyQualifiedFilePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Role
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw "The $Role path must not be empty."
    }

    if (-not [System.IO.Path]::IsPathRooted($Path) -or
        $Path -match '^[A-Za-z]:[^\\/]') {
        throw "The $Role path must be fully qualified."
    }

    return [System.IO.Path]::GetFullPath($Path)
}

function Assert-EqualValue {
    param(
        [AllowNull()]
        [object]$Actual,
        [AllowNull()]
        [object]$Expected,
        [Parameter(Mandatory = $true)]
        [string]$Role
    )

    if ($Actual -ne $Expected) {
        throw "The migrated Runtime Host profile did not preserve $Role."
    }
}

$installationDirectory = Join-Path $env:LOCALAPPDATA "HASE\RuntimeHost"
$applicationDirectory = Join-Path $installationDirectory "Application"
$configurationDirectory = Join-Path $installationDirectory "Configuration"
$identityDirectory = Join-Path $installationDirectory "Identity"
$executableFilePath = Join-Path $applicationDirectory "Hase.DesktopHost.App.exe"
$applicationProfilePath = Join-Path $configurationDirectory "desktop-runtime-host.json"
$endpointCompositionPath = Join-Path $configurationDirectory "desktop-runtime-endpoints.json"
$privateNetworkConfigurationPath = Join-Path $configurationDirectory "desktop-private-network.json"
$authorizationPolicyDestinationPath = Join-Path $configurationDirectory "runtime-host-authorization.json"
$profileBackupPath = $applicationProfilePath + ".49m-backup"
$temporaryProfilePath = $applicationProfilePath + ".49m-tmp"
$desktopDirectory = [Environment]::GetFolderPath(
    [Environment+SpecialFolder]::Desktop)
$shortcutPath = Join-Path $desktopDirectory "HASE Runtime Host.lnk"

if ($null -ne (Get-Process -Name "Hase.DesktopHost.App" -ErrorAction SilentlyContinue)) {
    throw "Stop the HASE Desktop Runtime Host before migrating its profile."
}

$requiredFiles = @(
    $executableFilePath,
    $applicationProfilePath,
    $endpointCompositionPath,
    $privateNetworkConfigurationPath,
    $shortcutPath
)
foreach ($requiredFile in $requiredFiles) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "The guided Runtime Host installation is incomplete."
    }
}
if (-not (Test-Path -LiteralPath $identityDirectory -PathType Container)) {
    throw "The guided Runtime Host installation is incomplete."
}

foreach ($protectedTarget in @(
        $authorizationPolicyDestinationPath,
        $profileBackupPath,
        $temporaryProfilePath)) {
    if (Test-Path -LiteralPath $protectedTarget) {
        throw "The Runtime Host migration target is not clean."
    }
}

$authorizationPolicySourcePath = Get-FullyQualifiedFilePath `
    -Path $AuthorizationPolicyPath `
    -Role "authorization-policy source"
if (-not (Test-Path -LiteralPath $authorizationPolicySourcePath -PathType Leaf)) {
    throw "The selected authorization-policy source file does not exist."
}
if ([string]::Equals(
        $authorizationPolicySourcePath,
        [System.IO.Path]::GetFullPath($authorizationPolicyDestinationPath),
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "The authorization-policy source must be outside the migration destination."
}

$policyFile = Get-Item -LiteralPath $authorizationPolicySourcePath
if ($policyFile.Length -gt (64 * 1024)) {
    throw "The authorization-policy source exceeds the supported size."
}
try {
    $policyDocument = Get-Content `
        -LiteralPath $authorizationPolicySourcePath `
        -Raw `
        -Encoding UTF8 | ConvertFrom-Json
}
catch {
    throw "The authorization-policy source is not valid JSON configuration."
}
if ($null -eq $policyDocument) {
    throw "The authorization-policy source does not have the supported structure."
}
$policyPropertyNames = @($policyDocument.PSObject.Properties.Name)
if ($policyPropertyNames.Count -ne 2 -or
    $policyPropertyNames -notcontains "formatVersion" -or
    $policyPropertyNames -notcontains "grants" -or
    $policyDocument.formatVersion -ne 1 -or
    $policyDocument.grants -isnot [System.Array]) {
    throw "The authorization-policy source does not have the supported structure."
}
$authorizationPolicySourceHash = (
    Get-FileHash `
        -LiteralPath $authorizationPolicySourcePath `
        -Algorithm SHA256).Hash

try {
    $originalProfile = Get-Content `
        -LiteralPath $applicationProfilePath `
        -Raw `
        -Encoding UTF8 | ConvertFrom-Json
}
catch {
    throw "The installed Runtime Host profile is not valid JSON configuration."
}
if ($null -eq $originalProfile) {
    throw "The installed Runtime Host profile is incomplete."
}
$profilePropertyNames = @($originalProfile.PSObject.Properties.Name)
$allowedProfilePropertyNames = @(
    "formatVersion",
    "identityFilePath",
    "privateNetworkConfigurationFilePath",
    "endpointCompositionFilePath",
    "maximumDiagnosticLevel",
    "includeByteBufferSimulation",
    "remoteDiagnosticsEnabled",
    "remoteDiagnosticsMaximumLevel",
    "authorizationPolicyFilePath"
)
foreach ($propertyName in $profilePropertyNames) {
    if ($allowedProfilePropertyNames -notcontains $propertyName) {
        throw "The installed Runtime Host profile has unsupported configuration."
    }
}
$requiredProfilePropertyNames = @(
    "formatVersion",
    "identityFilePath",
    "privateNetworkConfigurationFilePath",
    "endpointCompositionFilePath",
    "maximumDiagnosticLevel",
    "includeByteBufferSimulation"
)
foreach ($requiredPropertyName in $requiredProfilePropertyNames) {
    if ($profilePropertyNames -notcontains $requiredPropertyName) {
        throw "The installed Runtime Host profile is incomplete."
    }
}
if ($originalProfile.formatVersion -ne 1) {
    throw "The installed Runtime Host profile version is unsupported."
}
if ($profilePropertyNames -contains "authorizationPolicyFilePath") {
    throw "The installed Runtime Host profile has already been migrated."
}
if ($profilePropertyNames -contains "remoteDiagnosticsEnabled" -and
    $originalProfile.remoteDiagnosticsEnabled -eq $true) {
    throw "The installed Runtime Host profile already enables remote diagnostics."
}

$migratedProfile = $originalProfile
if ($profilePropertyNames -contains "remoteDiagnosticsEnabled") {
    $migratedProfile.remoteDiagnosticsEnabled = $true
}
else {
    $migratedProfile | Add-Member `
        -NotePropertyName "remoteDiagnosticsEnabled" `
        -NotePropertyValue $true
}
if ($profilePropertyNames -contains "remoteDiagnosticsMaximumLevel") {
    $migratedProfile.remoteDiagnosticsMaximumLevel =
        $RemoteDiagnosticsMaximumLevel
}
else {
    $migratedProfile | Add-Member `
        -NotePropertyName "remoteDiagnosticsMaximumLevel" `
        -NotePropertyValue $RemoteDiagnosticsMaximumLevel
}
$migratedProfile | Add-Member `
    -NotePropertyName "authorizationPolicyFilePath" `
    -NotePropertyValue $authorizationPolicyDestinationPath

$migratedProfileDocument = $migratedProfile | ConvertTo-Json -Depth 8
$verifiedGeneratedProfile = $migratedProfileDocument | ConvertFrom-Json
Assert-EqualValue $verifiedGeneratedProfile.identityFilePath $originalProfile.identityFilePath "identity custody"
Assert-EqualValue $verifiedGeneratedProfile.privateNetworkConfigurationFilePath $originalProfile.privateNetworkConfigurationFilePath "private-network custody"
Assert-EqualValue $verifiedGeneratedProfile.endpointCompositionFilePath $originalProfile.endpointCompositionFilePath "endpoint-composition custody"
Assert-EqualValue $verifiedGeneratedProfile.maximumDiagnosticLevel $originalProfile.maximumDiagnosticLevel "local diagnostic level"
Assert-EqualValue $verifiedGeneratedProfile.includeByteBufferSimulation $originalProfile.includeByteBufferSimulation "byte-buffer simulation state"

$policyCopied = $false
$profileReplaced = $false
try {
    Set-Content `
        -LiteralPath $temporaryProfilePath `
        -Value $migratedProfileDocument `
        -Encoding UTF8
    Copy-Item `
        -LiteralPath $authorizationPolicySourcePath `
        -Destination $authorizationPolicyDestinationPath
    $policyCopied = $true
    $authorizationPolicyDestinationHash = (
        Get-FileHash `
            -LiteralPath $authorizationPolicyDestinationPath `
            -Algorithm SHA256).Hash
    if ($authorizationPolicyDestinationHash -ne
        $authorizationPolicySourceHash) {
        throw "The installed authorization policy did not match its validated source."
    }

    [System.IO.File]::Replace(
        $temporaryProfilePath,
        $applicationProfilePath,
        $profileBackupPath,
        $true)
    $profileReplaced = $true

    $installedProfile = Get-Content `
        -LiteralPath $applicationProfilePath `
        -Raw `
        -Encoding UTF8 | ConvertFrom-Json
    Assert-EqualValue $installedProfile.identityFilePath $originalProfile.identityFilePath "identity custody"
    Assert-EqualValue $installedProfile.privateNetworkConfigurationFilePath $originalProfile.privateNetworkConfigurationFilePath "private-network custody"
    Assert-EqualValue $installedProfile.endpointCompositionFilePath $originalProfile.endpointCompositionFilePath "endpoint-composition custody"
    Assert-EqualValue $installedProfile.maximumDiagnosticLevel $originalProfile.maximumDiagnosticLevel "local diagnostic level"
    Assert-EqualValue $installedProfile.includeByteBufferSimulation $originalProfile.includeByteBufferSimulation "byte-buffer simulation state"
    Assert-EqualValue $installedProfile.remoteDiagnosticsEnabled $true "remote diagnostics state"
    Assert-EqualValue $installedProfile.remoteDiagnosticsMaximumLevel $RemoteDiagnosticsMaximumLevel "remote diagnostic level"
    Assert-EqualValue $installedProfile.authorizationPolicyFilePath $authorizationPolicyDestinationPath "authorization-policy custody"
}
catch {
    if ($profileReplaced -and
        (Test-Path -LiteralPath $profileBackupPath -PathType Leaf)) {
        [System.IO.File]::Replace(
            $profileBackupPath,
            $applicationProfilePath,
            $null,
            $true)
    }
    if ($policyCopied -and
        (Test-Path -LiteralPath $authorizationPolicyDestinationPath)) {
        Remove-Item -LiteralPath $authorizationPolicyDestinationPath -Force
    }
    if (Test-Path -LiteralPath $temporaryProfilePath) {
        Remove-Item -LiteralPath $temporaryProfilePath -Force
    }
    if (Test-Path -LiteralPath $profileBackupPath) {
        Remove-Item -LiteralPath $profileBackupPath -Force
    }
    throw
}

Write-Host "HASE Desktop Runtime Host remote diagnostics migration succeeded."
Write-Host "Application profile : migrated"
Write-Host "Original profile    : backup retained"
Write-Host "Authorization policy: installed"
Write-Host "Remote diagnostics  : enabled"
Write-Host "Sensitive values    : withheld"
