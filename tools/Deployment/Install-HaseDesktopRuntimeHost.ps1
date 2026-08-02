[CmdletBinding()]
param(
    [ValidateSet("DefaultPhysical", "CompactSerialOnly")]
    [string]$EndpointCompositionMode = "DefaultPhysical",
    [string]$CompactExpectedEndpointId = "arduino-uno-01",
    [string]$CompactVendorId = "0x2341",
    [string]$CompactProductId = "0x0043",
    [int]$CompactBaudRate = 115200,
    [int]$CompactVerificationTimeoutMilliseconds = 3000
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
$identityFilePath = Join-Path $identityDirectory "runtime-host-identity.json"
$desktopDirectory = [Environment]::GetFolderPath(
    [Environment+SpecialFolder]::Desktop)
$shortcutPath = Join-Path $desktopDirectory "HASE Runtime Host.lnk"

$protectedTargets = @(
    $applicationProfilePath,
    $endpointCompositionPath,
    $privateNetworkDestinationPath,
    $shortcutPath
)

foreach ($target in $protectedTargets) {
    if (Test-Path -LiteralPath $target) {
        throw "A guided Runtime Host installation already exists. Existing profiles and shortcuts are not overwritten."
    }
}

$privateNetworkSourceInput = Read-Host `
    "Fully qualified path to the existing desktop private-network JSON file"
$privateNetworkSourcePath = Get-FullyQualifiedFilePath `
    -Path $privateNetworkSourceInput `
    -Role "private-network configuration source"

if (-not (Test-Path -LiteralPath $privateNetworkSourcePath -PathType Leaf)) {
    throw "The selected private-network configuration source file does not exist."
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
}

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
