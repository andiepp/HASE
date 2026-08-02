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

$publisherPath = Join-Path $PSScriptRoot "Publish-HaseClient.ps1"
if (-not (Test-Path -LiteralPath $publisherPath -PathType Leaf)) {
    throw "The lower-level HASE Client publisher was not found."
}

$installationDirectory = Join-Path $env:LOCALAPPDATA "HASE\Client"
$applicationDirectory = Join-Path $installationDirectory "Application"
$configurationDirectory = Join-Path $installationDirectory "Configuration"
$executableFilePath = Join-Path $applicationDirectory "Hase.Client.Wpf.App.exe"
$configurationFilePath = Join-Path $configurationDirectory "laptop-private-network.json"
$runtimeHostRegistryFilePath = Join-Path $configurationDirectory "client-runtime-hosts.json"
$desktopDirectory = [Environment]::GetFolderPath(
    [Environment+SpecialFolder]::Desktop)
$shortcutPath = Join-Path $desktopDirectory "HASE Client.lnk"

if (Test-Path -LiteralPath $configurationFilePath) {
    throw "A guided HASE Client configuration already exists and will not be overwritten."
}

if (Test-Path -LiteralPath $runtimeHostRegistryFilePath) {
    throw "A guided HASE Client Runtime Host registry already exists and will not be overwritten."
}

if (Test-Path -LiteralPath $shortcutPath) {
    throw "A HASE Client desktop shortcut already exists and will not be overwritten."
}

$configurationSourceInput = Read-Host `
    "Fully qualified path to the existing laptop private-network JSON file"
$configurationSourcePath = Get-FullyQualifiedFilePath `
    -Path $configurationSourceInput `
    -Role "client configuration source"

if (-not (Test-Path -LiteralPath $configurationSourcePath -PathType Leaf)) {
    throw "The selected client configuration source file does not exist."
}

$profileId = (Read-Host "Client-local Runtime Host profile ID").Trim()
if ($profileId -notmatch '^[a-z0-9][a-z0-9._-]{0,63}$') {
    throw "The profile ID must begin with a lowercase letter or digit and contain only lowercase letters, digits, '.', '_', or '-'."
}

$displayName = (Read-Host "Runtime Host display name").Trim()
if ([string]::IsNullOrWhiteSpace($displayName) -or $displayName.Length -gt 256) {
    throw "The Runtime Host display name must contain between 1 and 256 characters."
}

$expectedRuntimeHostId = (Read-Host "Expected authoritative Runtime Host ID").Trim()
if ([string]::IsNullOrWhiteSpace($expectedRuntimeHostId)) {
    throw "The expected authoritative Runtime Host ID must not be empty."
}

& $publisherPath -InstallationDirectory $installationDirectory

if (-not (Test-Path -LiteralPath $executableFilePath -PathType Leaf)) {
    throw "The published HASE Client executable was not found."
}

$configurationInstalled = $false
try {
    Copy-Item `
        -LiteralPath $configurationSourcePath `
        -Destination $configurationFilePath
    $configurationInstalled = $true

    $registryDocument = [ordered]@{
        formatVersion = 1
        hosts = @(
            [ordered]@{
                profileId = $profileId
                displayName = $displayName
                expectedRuntimeHostId = $expectedRuntimeHostId
                privateNetworkConfigurationFilePath = $configurationFilePath
                enabled = $true
            }
        )
    }
    $registryDocument |
        ConvertTo-Json -Depth 4 |
        Set-Content -LiteralPath $runtimeHostRegistryFilePath -Encoding utf8

    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $executableFilePath
    $shortcut.Arguments = '"' + $runtimeHostRegistryFilePath + '"'
    $shortcut.WorkingDirectory = $applicationDirectory
    $shortcut.IconLocation = $executableFilePath
    $shortcut.Description = "HASE Client"
    $shortcut.Save()

    if (-not (Test-Path -LiteralPath $shortcutPath -PathType Leaf)) {
        throw "The HASE Client desktop shortcut could not be verified."
    }
}
catch {
    if (Test-Path -LiteralPath $shortcutPath) {
        Remove-Item -LiteralPath $shortcutPath -Force
    }

    if ($configurationInstalled -and
        (Test-Path -LiteralPath $configurationFilePath)) {
        Remove-Item -LiteralPath $configurationFilePath -Force
    }

    if (Test-Path -LiteralPath $runtimeHostRegistryFilePath) {
        Remove-Item -LiteralPath $runtimeHostRegistryFilePath -Force
    }

    throw
}

Write-Host "HASE Client guided installation succeeded."
Write-Host "Installation directory: $installationDirectory"
Write-Host "Client configuration : $configurationFilePath"
Write-Host "Runtime Host registry: $runtimeHostRegistryFilePath"
Write-Host "Desktop shortcut     : $shortcutPath"
Write-Host "Startup arguments    : one Runtime Host registry path"
