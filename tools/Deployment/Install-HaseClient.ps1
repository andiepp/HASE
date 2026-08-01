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
$desktopDirectory = [Environment]::GetFolderPath(
    [Environment+SpecialFolder]::Desktop)
$shortcutPath = Join-Path $desktopDirectory "HASE Client.lnk"

if (Test-Path -LiteralPath $configurationFilePath) {
    throw "A guided HASE Client configuration already exists and will not be overwritten."
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

    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $executableFilePath
    $shortcut.Arguments = '"' + $configurationFilePath + '"'
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

    throw
}

Write-Host "HASE Client guided installation succeeded."
Write-Host "Installation directory: $installationDirectory"
Write-Host "Client configuration : $configurationFilePath"
Write-Host "Desktop shortcut     : $shortcutPath"
Write-Host "Startup arguments    : one client-configuration path"
