[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$installationDirectory = Join-Path $env:LOCALAPPDATA "HASE\Client"
$applicationDirectory = Join-Path $installationDirectory "Application"
$configurationDirectory = Join-Path $installationDirectory "Configuration"
$executableFilePath = Join-Path $applicationDirectory "Hase.Client.Wpf.App.exe"
$configurationFilePath = Join-Path $configurationDirectory "laptop-private-network.json"
$runtimeHostRegistryFilePath = Join-Path $configurationDirectory "client-runtime-hosts.json"
$desktopDirectory = [Environment]::GetFolderPath(
    [Environment+SpecialFolder]::Desktop)
$shortcutPath = Join-Path $desktopDirectory "HASE Client.lnk"
$shortcutBackupPath = $shortcutPath + ".43f-backup"

$runningClient = Get-Process `
    -Name "Hase.Client.Wpf.App" `
    -ErrorAction SilentlyContinue
if ($null -ne $runningClient) {
    throw "HASE Client is running. Close it before migrating configuration custody."
}

foreach ($requiredFile in @(
        $executableFilePath,
        $configurationFilePath,
        $shortcutPath)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "The existing guided HASE Client installation is incomplete: $requiredFile"
    }
}

if (Test-Path -LiteralPath $runtimeHostRegistryFilePath) {
    throw "The HASE Client Runtime Host registry already exists and will not be overwritten."
}

if (Test-Path -LiteralPath $shortcutBackupPath) {
    throw "A previous migration backup exists. Resolve it before retrying: $shortcutBackupPath"
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
$expectedShortcutArguments = '"' + $runtimeHostRegistryFilePath + '"'

$backupCreated = $false
try {
    $registryDocument |
        ConvertTo-Json -Depth 4 |
        Set-Content -LiteralPath $runtimeHostRegistryFilePath -Encoding utf8
    Copy-Item -LiteralPath $shortcutPath -Destination $shortcutBackupPath
    $backupCreated = $true

    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $executableFilePath
    $shortcut.Arguments = $expectedShortcutArguments
    $shortcut.WorkingDirectory = $applicationDirectory
    $shortcut.IconLocation = $executableFilePath
    $shortcut.Description = "HASE Client"
    $shortcut.Save()

    $verifiedShortcut = $shell.CreateShortcut($shortcutPath)
    if (-not [string]::Equals(
            $verifiedShortcut.Arguments,
            $expectedShortcutArguments,
            [System.StringComparison]::Ordinal)) {
        throw "The migrated HASE Client shortcut could not be verified."
    }

    Remove-Item -LiteralPath $shortcutBackupPath -Force
    $backupCreated = $false
}
catch {
    if ($backupCreated -and
        (Test-Path -LiteralPath $shortcutBackupPath -PathType Leaf)) {
        Copy-Item `
            -LiteralPath $shortcutBackupPath `
            -Destination $shortcutPath `
            -Force
        Remove-Item -LiteralPath $shortcutBackupPath -Force
    }

    if (Test-Path -LiteralPath $runtimeHostRegistryFilePath -PathType Leaf) {
        Remove-Item -LiteralPath $runtimeHostRegistryFilePath -Force
    }

    throw
}

Write-Host "HASE Client Runtime Host registry migration succeeded."
Write-Host "Runtime Host profile : $displayName"
Write-Host "Runtime Host registry: $runtimeHostRegistryFilePath"
Write-Host "Client configuration : preserved"
Write-Host "Desktop shortcut     : migrated"
