[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Read-JsonConfiguration {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Role
    )

    try {
        $document = Get-Content `
            -LiteralPath $Path `
            -Raw `
            -Encoding UTF8 | ConvertFrom-Json
    }
    catch {
        throw "The $Role is not valid JSON configuration."
    }

    if ($null -eq $document) {
        throw "The $Role does not have the supported structure."
    }

    return $document
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
        throw "The rollback profiles do not have matching $Role."
    }
}

function Assert-EqualPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Actual,
        [Parameter(Mandatory = $true)]
        [string]$Expected,
        [Parameter(Mandatory = $true)]
        [string]$Role
    )

    if ([string]::IsNullOrWhiteSpace($Actual) -or
        -not [string]::Equals(
            [System.IO.Path]::GetFullPath($Actual),
            [System.IO.Path]::GetFullPath($Expected),
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "The rollback $Role does not match the guided installation."
    }
}

function Assert-SupportedProfile {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Profile,
        [Parameter(Mandatory = $true)]
        [string]$Role
    )

    $propertyNames = @($Profile.PSObject.Properties.Name)
    $allowedPropertyNames = @(
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
    foreach ($propertyName in $propertyNames) {
        if ($allowedPropertyNames -notcontains $propertyName) {
            throw "The $Role has unsupported configuration."
        }
    }

    foreach ($requiredPropertyName in @(
            "formatVersion",
            "identityFilePath",
            "privateNetworkConfigurationFilePath",
            "endpointCompositionFilePath",
            "maximumDiagnosticLevel",
            "includeByteBufferSimulation")) {
        if ($propertyNames -notcontains $requiredPropertyName) {
            throw "The $Role is incomplete."
        }
    }

    if ($Profile.formatVersion -ne 1) {
        throw "The $Role version is unsupported."
    }

    return $propertyNames
}

$installationDirectory = Join-Path $env:LOCALAPPDATA "HASE\RuntimeHost"
$applicationDirectory = Join-Path $installationDirectory "Application"
$configurationDirectory = Join-Path $installationDirectory "Configuration"
$identityDirectory = Join-Path $installationDirectory "Identity"
$executableFilePath = Join-Path $applicationDirectory "Hase.DesktopHost.App.exe"
$applicationProfilePath = Join-Path $configurationDirectory "desktop-runtime-host.json"
$endpointCompositionPath = Join-Path $configurationDirectory "desktop-runtime-endpoints.json"
$privateNetworkConfigurationPath = Join-Path $configurationDirectory "desktop-private-network.json"
$authorizationPolicyPath = Join-Path $configurationDirectory "runtime-host-authorization.json"
$identityFilePath = Join-Path $identityDirectory "runtime-host-identity.json"
$originalProfileBackupPath = $applicationProfilePath + ".49m-backup"
$migratedProfileBackupPath = $applicationProfilePath + ".49n-migrated-backup"
$migrationTemporaryProfilePath = $applicationProfilePath + ".49m-tmp"
$rollbackTemporaryProfilePath = $applicationProfilePath + ".49n-tmp"
$desktopDirectory = [Environment]::GetFolderPath(
    [Environment+SpecialFolder]::Desktop)
$shortcutPath = Join-Path $desktopDirectory "HASE Runtime Host.lnk"

if ($null -ne (Get-Process -Name "Hase.DesktopHost.App" -ErrorAction SilentlyContinue)) {
    throw "Stop the HASE Desktop Runtime Host before restoring its profile."
}

foreach ($requiredFile in @(
        $executableFilePath,
        $applicationProfilePath,
        $endpointCompositionPath,
        $privateNetworkConfigurationPath,
        $authorizationPolicyPath,
        $originalProfileBackupPath,
        $shortcutPath)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "The guided Runtime Host migration rollback prerequisites are incomplete."
    }
}
if (-not (Test-Path -LiteralPath $identityDirectory -PathType Container)) {
    throw "The guided Runtime Host migration rollback prerequisites are incomplete."
}
foreach ($prohibitedArtifact in @(
        $migratedProfileBackupPath,
        $migrationTemporaryProfilePath,
        $rollbackTemporaryProfilePath)) {
    if (Test-Path -LiteralPath $prohibitedArtifact) {
        throw "The Runtime Host migration rollback target is not clean."
    }
}

$policyFile = Get-Item -LiteralPath $authorizationPolicyPath
if ($policyFile.Length -gt (64 * 1024)) {
    throw "The installed authorization policy exceeds the supported size."
}
$policyDocument = Read-JsonConfiguration `
    -Path $authorizationPolicyPath `
    -Role "installed authorization policy"
$policyPropertyNames = @($policyDocument.PSObject.Properties.Name)
if ($policyPropertyNames.Count -ne 2 -or
    $policyPropertyNames -notcontains "formatVersion" -or
    $policyPropertyNames -notcontains "grants" -or
    $policyDocument.formatVersion -ne 1 -or
    $policyDocument.grants -isnot [System.Array]) {
    throw "The installed authorization policy does not have the supported structure."
}
$authorizationPolicyHash = (
    Get-FileHash -LiteralPath $authorizationPolicyPath -Algorithm SHA256).Hash

$activeProfile = Read-JsonConfiguration `
    -Path $applicationProfilePath `
    -Role "active Runtime Host profile"
$activePropertyNames = Assert-SupportedProfile `
    -Profile $activeProfile `
    -Role "active Runtime Host profile"
if ($activePropertyNames -notcontains "remoteDiagnosticsEnabled" -or
    $activeProfile.remoteDiagnosticsEnabled -ne $true -or
    $activePropertyNames -notcontains "remoteDiagnosticsMaximumLevel" -or
    $activePropertyNames -notcontains "authorizationPolicyFilePath") {
    throw "The active Runtime Host profile is not a completed remote-diagnostics migration."
}
if ($activeProfile.remoteDiagnosticsMaximumLevel -notin @(
        "Operational",
        "Protocol",
        "Bytes")) {
    throw "The active Runtime Host profile has an unsupported remote diagnostic level."
}
Assert-EqualPath `
    -Actual $activeProfile.authorizationPolicyFilePath `
    -Expected $authorizationPolicyPath `
    -Role "authorization-policy path"
Assert-EqualPath `
    -Actual $activeProfile.identityFilePath `
    -Expected $identityFilePath `
    -Role "identity path"
Assert-EqualPath `
    -Actual $activeProfile.privateNetworkConfigurationFilePath `
    -Expected $privateNetworkConfigurationPath `
    -Role "private-network path"
Assert-EqualPath `
    -Actual $activeProfile.endpointCompositionFilePath `
    -Expected $endpointCompositionPath `
    -Role "endpoint-composition path"

$originalProfile = Read-JsonConfiguration `
    -Path $originalProfileBackupPath `
    -Role "original Runtime Host profile backup"
$originalPropertyNames = Assert-SupportedProfile `
    -Profile $originalProfile `
    -Role "original Runtime Host profile backup"
if ($originalPropertyNames -contains "authorizationPolicyFilePath" -or
    ($originalPropertyNames -contains "remoteDiagnosticsEnabled" -and
        $originalProfile.remoteDiagnosticsEnabled -eq $true)) {
    throw "The original Runtime Host profile backup is not a disabled pre-migration profile."
}

Assert-EqualValue $activeProfile.identityFilePath $originalProfile.identityFilePath "identity custody"
Assert-EqualValue $activeProfile.privateNetworkConfigurationFilePath $originalProfile.privateNetworkConfigurationFilePath "private-network custody"
Assert-EqualValue $activeProfile.endpointCompositionFilePath $originalProfile.endpointCompositionFilePath "endpoint-composition custody"
Assert-EqualValue $activeProfile.maximumDiagnosticLevel $originalProfile.maximumDiagnosticLevel "local diagnostic level"
Assert-EqualValue $activeProfile.includeByteBufferSimulation $originalProfile.includeByteBufferSimulation "byte-buffer simulation state"

$originalProfileHash = (
    Get-FileHash -LiteralPath $originalProfileBackupPath -Algorithm SHA256).Hash
$profileReplaced = $false
try {
    [System.IO.File]::Replace(
        $originalProfileBackupPath,
        $applicationProfilePath,
        $migratedProfileBackupPath,
        $true)
    $profileReplaced = $true

    $restoredProfileHash = (
        Get-FileHash -LiteralPath $applicationProfilePath -Algorithm SHA256).Hash
    if ($restoredProfileHash -ne $originalProfileHash) {
        throw "The restored Runtime Host profile bytes do not match the original backup."
    }
    if ($authorizationPolicyHash -ne (
            Get-FileHash `
                -LiteralPath $authorizationPolicyPath `
                -Algorithm SHA256).Hash) {
        throw "The migration rollback changed authorization-policy custody."
    }

    $restoredProfile = Read-JsonConfiguration `
        -Path $applicationProfilePath `
        -Role "restored Runtime Host profile"
    $restoredPropertyNames = Assert-SupportedProfile `
        -Profile $restoredProfile `
        -Role "restored Runtime Host profile"
    if ($restoredPropertyNames -contains "authorizationPolicyFilePath" -or
        ($restoredPropertyNames -contains "remoteDiagnosticsEnabled" -and
            $restoredProfile.remoteDiagnosticsEnabled -eq $true)) {
        throw "The restored Runtime Host profile still enables remote diagnostics."
    }
}
catch {
    if ($profileReplaced -and
        (Test-Path -LiteralPath $migratedProfileBackupPath -PathType Leaf)) {
        [System.IO.File]::Replace(
            $migratedProfileBackupPath,
            $applicationProfilePath,
            $originalProfileBackupPath,
            $true)
    }
    if (Test-Path -LiteralPath $rollbackTemporaryProfilePath) {
        Remove-Item -LiteralPath $rollbackTemporaryProfilePath -Force
    }
    throw
}

Write-Host "HASE Desktop Runtime Host remote diagnostics migration rollback succeeded."
Write-Host "Application profile : original restored"
Write-Host "Migrated profile    : backup retained"
Write-Host "Authorization policy: retained and inactive"
Write-Host "Remote diagnostics  : disabled"
Write-Host "Sensitive values    : withheld"
