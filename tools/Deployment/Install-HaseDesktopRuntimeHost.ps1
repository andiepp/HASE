[CmdletBinding()]
param(
    [ValidateSet("DefaultPhysical", "CompactSerialOnly")]
    [string]$EndpointCompositionMode = "DefaultPhysical",
    [string]$CompactExpectedEndpointId = "arduino-uno-01",
    [string]$CompactVendorId = "0x2341",
    [string]$CompactProductId = "0x0043",
    [int]$CompactBaudRate = 115200,
    [int]$CompactVerificationTimeoutMilliseconds = 3000,
    [string]$PrivateNetworkConfigurationPath,
    [switch]$EnableRemoteDiagnostics,
    [ValidateSet("Operational", "Protocol", "Bytes")]
    [string]$RemoteDiagnosticsMaximumLevel = "Operational",
    [string]$AuthorizationPolicyPath,
    [string]$MediaConfigurationPath
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

function ConvertFrom-ExactUsbIdentifier {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value,
        [Parameter(Mandatory = $true)]
        [string]$Role
    )

    if ($Value -notmatch '^0x[0-9A-Fa-f]{4}$') {
        throw "The $Role must use exact 0xNNNN hexadecimal form."
    }

    return [Convert]::ToUInt16($Value.Substring(2), 16)
}

$repositoryRoot = [System.IO.Path]::GetFullPath(
    (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))
$publisherPath = Join-Path $PSScriptRoot "Publish-HaseDesktopRuntimeHost.ps1"
if (-not (Test-Path -LiteralPath $publisherPath -PathType Leaf)) {
    throw "The lower-level Desktop Runtime Host publisher was not found."
}

$installationDirectory = Join-Path $env:LOCALAPPDATA "HASE\RuntimeHost"
$applicationDirectory = Join-Path $installationDirectory "Application"
$configurationDirectory = Join-Path $installationDirectory "Configuration"
$identityDirectory = Join-Path $installationDirectory "Identity"
$executableFilePath = Join-Path $applicationDirectory "Hase.DesktopHost.App.exe"
$applicationProfilePath = Join-Path $configurationDirectory "desktop-runtime-host.json"
$endpointCompositionPath = Join-Path $configurationDirectory "desktop-runtime-endpoints.json"
$privateNetworkDestinationPath = Join-Path $configurationDirectory "desktop-private-network.json"
$authorizationPolicyDestinationPath = Join-Path $configurationDirectory "runtime-host-authorization.json"
$mediaConfigurationDestinationPath = Join-Path $configurationDirectory "desktop-runtime-media.json"
$identityFilePath = Join-Path $identityDirectory "runtime-host-identity.json"
$desktopDirectory = [Environment]::GetFolderPath(
    [Environment+SpecialFolder]::Desktop)
$shortcutPath = Join-Path $desktopDirectory "HASE Runtime Host.lnk"

$protectedTargets = @(
    $applicationProfilePath,
    $endpointCompositionPath,
    $privateNetworkDestinationPath,
    $authorizationPolicyDestinationPath,
    $mediaConfigurationDestinationPath,
    $shortcutPath
)

foreach ($target in $protectedTargets) {
    if (Test-Path -LiteralPath $target) {
        throw "A guided Runtime Host installation already exists. Existing profiles and shortcuts are not overwritten."
    }
}

$privateNetworkSourceInput = if (
    [string]::IsNullOrWhiteSpace($PrivateNetworkConfigurationPath)
) {
    Read-Host `
        "Fully qualified path to the existing desktop private-network JSON file"
}
else {
    $PrivateNetworkConfigurationPath
}
$privateNetworkSourcePath = Get-FullyQualifiedFilePath `
    -Path $privateNetworkSourceInput `
    -Role "private-network configuration source"

if (-not (Test-Path -LiteralPath $privateNetworkSourcePath -PathType Leaf)) {
    throw "The selected private-network configuration source file does not exist."
}

$authorizationPolicySourcePath = $null
if ($PSBoundParameters.ContainsKey("AuthorizationPolicyPath") -and
    [string]::IsNullOrWhiteSpace($AuthorizationPolicyPath)) {
    throw "The authorization-policy source path must not be empty."
}

if (-not [string]::IsNullOrWhiteSpace($AuthorizationPolicyPath)) {
    $authorizationPolicySourcePath = Get-FullyQualifiedFilePath `
        -Path $AuthorizationPolicyPath `
        -Role "authorization-policy source"

    if (-not (Test-Path -LiteralPath $authorizationPolicySourcePath -PathType Leaf)) {
        throw "The selected authorization-policy source file does not exist."
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

    $policyPropertyNames = @(
        $policyDocument.PSObject.Properties.Name)
    if ($policyPropertyNames.Count -ne 2 -or
        $policyPropertyNames -notcontains "formatVersion" -or
        $policyPropertyNames -notcontains "grants" -or
        $policyDocument.formatVersion -ne 1 -or
        $policyDocument.grants -isnot [System.Array]) {
        throw "The authorization-policy source does not have the supported structure."
    }
}

if ($EnableRemoteDiagnostics -and $null -eq $authorizationPolicySourcePath) {
    throw "Remote diagnostics require an explicit authorization-policy source."
}

$mediaConfigurationSourcePath = $null
if ($PSBoundParameters.ContainsKey("MediaConfigurationPath")) {
    if ([string]::IsNullOrWhiteSpace($MediaConfigurationPath)) {
        throw "The media-configuration source path must not be empty."
    }
    if ($null -eq $authorizationPolicySourcePath) {
        throw "Runtime Host media requires an explicit authorization-policy source."
    }
    $mediaConfigurationSourcePath = Get-FullyQualifiedFilePath `
        -Path $MediaConfigurationPath `
        -Role "media-configuration source"
    if (-not (Test-Path -LiteralPath $mediaConfigurationSourcePath -PathType Leaf)) {
        throw "The selected media-configuration source file does not exist."
    }
    $mediaFile = Get-Item -LiteralPath $mediaConfigurationSourcePath
    if ($mediaFile.Length -gt (64 * 1024)) {
        throw "The media-configuration source exceeds the supported size."
    }
    try {
        $mediaDocument = Get-Content -LiteralPath $mediaConfigurationSourcePath `
            -Raw -Encoding UTF8 | ConvertFrom-Json
    }
    catch {
        throw "The media-configuration source is not valid JSON configuration."
    }
    if ($null -eq $mediaDocument) {
        throw "The media-configuration source does not have the supported structure."
    }
    $mediaPropertyNames = @($mediaDocument.PSObject.Properties.Name)
    if ($mediaPropertyNames.Count -ne 2 -or
        $mediaPropertyNames -notcontains "formatVersion" -or
        $mediaPropertyNames -notcontains "sources" -or
        $mediaDocument.formatVersion -ne 1 -or
        $mediaDocument.sources -isnot [System.Array] -or
        $mediaDocument.sources.Count -lt 1 -or
        $mediaDocument.sources.Count -gt 16) {
        throw "The media-configuration source does not have the supported structure."
    }
}

if (-not $EnableRemoteDiagnostics -and
    $PSBoundParameters.ContainsKey("RemoteDiagnosticsMaximumLevel")) {
    throw "A remote diagnostics maximum level requires explicit remote diagnostics enablement."
}

$vendorId = ConvertFrom-ExactUsbIdentifier `
    -Value $CompactVendorId `
    -Role "USB vendor ID"
$productId = ConvertFrom-ExactUsbIdentifier `
    -Value $CompactProductId `
    -Role "USB product ID"
if ([string]::IsNullOrWhiteSpace($CompactExpectedEndpointId)) {
    throw "The compact endpoint identity must not be empty."
}
if ($CompactBaudRate -le 0) {
    throw "The compact baud rate must be positive."
}
if ($CompactVerificationTimeoutMilliseconds -lt 1 -or
    $CompactVerificationTimeoutMilliseconds -gt 60000) {
    throw "The compact verification timeout must be between 1 and 60000 milliseconds."
}

$configuredEndpoints = New-Object System.Collections.Generic.List[object]
if ($EndpointCompositionMode -eq "DefaultPhysical") {
    $nativeEndpointHost = Read-Host "ESP32 host name or address"
    if ([string]::IsNullOrWhiteSpace($nativeEndpointHost)) {
        throw "The ESP32 host name or address must not be empty."
    }
    $configuredEndpoints.Add([ordered]@{
        kind = "NativeNetwork"
        expectedEndpointId = "doit-esp32-devkitc-v4-01"
        host = $nativeEndpointHost.Trim()
        port = 5000
    })
}

$configuredEndpoints.Add([ordered]@{
    kind = "CompactSerial"
    expectedEndpointId = $CompactExpectedEndpointId.Trim()
    vendorId = $vendorId
    productId = $productId
    baudRate = $CompactBaudRate
    verificationTimeoutMilliseconds = $CompactVerificationTimeoutMilliseconds
})

$applicationProfile = [ordered]@{
    formatVersion = 1
    identityFilePath = $identityFilePath
    privateNetworkConfigurationFilePath = $privateNetworkDestinationPath
    endpointCompositionFilePath = $endpointCompositionPath
    maximumDiagnosticLevel = "Bytes"
    includeByteBufferSimulation = $false
    remoteDiagnosticsEnabled = $false
    remoteDiagnosticsMaximumLevel = if ($EnableRemoteDiagnostics) {
        $RemoteDiagnosticsMaximumLevel
    }
    else {
        "Operational"
    }
}

if ($null -ne $authorizationPolicySourcePath) {
    $applicationProfile.authorizationPolicyFilePath =
        $authorizationPolicyDestinationPath
}
if ($null -ne $mediaConfigurationSourcePath) {
    $applicationProfile.mediaConfigurationFilePath =
        $mediaConfigurationDestinationPath
}
$applicationProfile.remoteDiagnosticsEnabled = [bool]$EnableRemoteDiagnostics

$endpointComposition = [ordered]@{
    formatVersion = 1
    endpoints = $configuredEndpoints.ToArray()
}

$applicationDocument = $applicationProfile | ConvertTo-Json -Depth 8
$endpointDocument = $endpointComposition | ConvertTo-Json -Depth 8

# Parse the generated text before performing publication or installation.
$null = $applicationDocument | ConvertFrom-Json
$null = $endpointDocument | ConvertFrom-Json

& $publisherPath -InstallationDirectory $installationDirectory

if (-not (Test-Path -LiteralPath $executableFilePath -PathType Leaf)) {
    throw "The published Desktop Runtime Host executable was not found."
}

$installedFiles = New-Object System.Collections.Generic.List[string]
try {
    Copy-Item `
        -LiteralPath $privateNetworkSourcePath `
        -Destination $privateNetworkDestinationPath
    $installedFiles.Add($privateNetworkDestinationPath)

    if ($null -ne $authorizationPolicySourcePath) {
        Copy-Item `
            -LiteralPath $authorizationPolicySourcePath `
            -Destination $authorizationPolicyDestinationPath
        $installedFiles.Add($authorizationPolicyDestinationPath)
    }

    if ($null -ne $mediaConfigurationSourcePath) {
        Copy-Item -LiteralPath $mediaConfigurationSourcePath `
            -Destination $mediaConfigurationDestinationPath
        $installedFiles.Add($mediaConfigurationDestinationPath)
    }

    Set-Content `
        -LiteralPath $endpointCompositionPath `
        -Value $endpointDocument `
        -Encoding UTF8
    $installedFiles.Add($endpointCompositionPath)

    Set-Content `
        -LiteralPath $applicationProfilePath `
        -Value $applicationDocument `
        -Encoding UTF8
    $installedFiles.Add($applicationProfilePath)

    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $executableFilePath
    $shortcut.Arguments = '"' + $applicationProfilePath + '"'
    $shortcut.WorkingDirectory = $applicationDirectory
    $shortcut.IconLocation = $executableFilePath
    $shortcut.Description = "HASE Desktop Runtime Host"
    $shortcut.Save()

    if (-not (Test-Path -LiteralPath $shortcutPath -PathType Leaf)) {
        throw "The HASE Runtime Host desktop shortcut could not be verified."
    }
}
catch {
    if (Test-Path -LiteralPath $shortcutPath) {
        Remove-Item -LiteralPath $shortcutPath -Force
    }

    foreach ($installedFile in $installedFiles) {
        if (Test-Path -LiteralPath $installedFile) {
            Remove-Item -LiteralPath $installedFile -Force
        }
    }

    throw
}

Write-Host "HASE Desktop Runtime Host guided installation succeeded."
Write-Host "Installation directory: $installationDirectory"
Write-Host "Application profile  : $applicationProfilePath"
Write-Host "Identity file        : $identityFilePath"
Write-Host "Desktop shortcut     : $shortcutPath"
Write-Host "Startup arguments    : one application-profile path"
Write-Host "Endpoint composition : $EndpointCompositionMode"
Write-Host "Remote diagnostics   : $([bool]$EnableRemoteDiagnostics)"
Write-Host "Authorization policy : $(if ($null -eq $authorizationPolicySourcePath) { 'not installed' } else { 'installed' })"
Write-Host "Media configuration   : $(if ($null -eq $mediaConfigurationSourcePath) { 'not installed' } else { 'installed' })"
