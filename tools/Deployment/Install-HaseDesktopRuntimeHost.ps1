[CmdletBinding()]
param()

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

$nativeEndpointHost = Read-Host "ESP32 host name or address"
if ([string]::IsNullOrWhiteSpace($nativeEndpointHost)) {
    throw "The ESP32 host name or address must not be empty."
}
$nativeEndpointHost = $nativeEndpointHost.Trim()

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
    endpoints = @(
        [ordered]@{
            kind = "NativeNetwork"
            expectedEndpointId = "doit-esp32-devkitc-v4-01"
            host = $nativeEndpointHost
            port = 5000
        },
        [ordered]@{
            kind = "CompactSerial"
            expectedEndpointId = "arduino-uno-01"
            vendorId = 0x2341
            productId = 0x0043
            baudRate = 115200
            verificationTimeoutMilliseconds = 3000
        }
    )
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
