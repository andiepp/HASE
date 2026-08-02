[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath(
    (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))
$auditProject = Join-Path $repositoryRoot `
    "src\Hase.DesktopHost.OnboardingAudit\Hase.DesktopHost.OnboardingAudit.csproj"
$installationDirectory = Join-Path $env:LOCALAPPDATA "HASE\RuntimeHost"
$applicationDirectory = Join-Path $installationDirectory "Application"
$configurationDirectory = Join-Path $installationDirectory "Configuration"
$executablePath = Join-Path $applicationDirectory "Hase.DesktopHost.App.exe"
$applicationProfilePath = Join-Path $configurationDirectory "desktop-runtime-host.json"
$desktopDirectory = [Environment]::GetFolderPath(
    [Environment+SpecialFolder]::Desktop)
$shortcutPath = Join-Path $desktopDirectory "HASE Runtime Host.lnk"

if (-not (Test-Path -LiteralPath $shortcutPath -PathType Leaf)) {
    throw "The installed Runtime Host desktop shortcut is missing."
}

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$expectedArguments = '"' + $applicationProfilePath + '"'

if (-not [string]::Equals(
        [System.IO.Path]::GetFullPath($shortcut.TargetPath),
        [System.IO.Path]::GetFullPath($executablePath),
        [System.StringComparison]::OrdinalIgnoreCase) -or
    -not [string]::Equals(
        [System.IO.Path]::GetFullPath($shortcut.WorkingDirectory),
        [System.IO.Path]::GetFullPath($applicationDirectory),
        [System.StringComparison]::OrdinalIgnoreCase) -or
    -not [string]::Equals(
        $shortcut.Arguments,
        $expectedArguments,
        [System.StringComparison]::Ordinal)) {
    throw "The installed Runtime Host desktop shortcut is inconsistent with guided installation custody."
}

& dotnet run `
    --project $auditProject `
    -c Release `
    --no-build `
    -- `
    $installationDirectory

if ($LASTEXITCODE -ne 0) {
    throw "The installed Runtime Host onboarding audit failed."
}

Write-Host "Desktop shortcut             : Ready"
Write-Host "Audit mode                   : Read only"
