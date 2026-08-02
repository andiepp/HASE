[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$DestinationPath
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if ([System.Environment]::OSVersion.Platform -ne [System.PlatformID]::Win32NT) {
    throw "The MiniPC Runtime Host handoff workflow requires Windows."
}
if (@(Get-Process -Name "Hase.DesktopHost.App" -ErrorAction SilentlyContinue).Count -ne 0) {
    throw "The MiniPC Runtime Host must be stopped before creating its handoff."
}
if (-not [System.IO.Path]::IsPathRooted($DestinationPath) -or
    $DestinationPath -match '^[A-Za-z]:[^\\/]') {
    throw "The MiniPC handoff destination path must be fully qualified."
}

$destination = [System.IO.Path]::GetFullPath($DestinationPath)
$handoffScript = Join-Path $PSScriptRoot "New-HaseRuntimeHostOnboardingHandoff.ps1"
if (-not (Test-Path -LiteralPath $handoffScript -PathType Leaf)) {
    throw "The strict Runtime Host handoff tool was not found."
}

$null = & $handoffScript -DestinationPath $destination *>&1
if (-not (Test-Path -LiteralPath $destination -PathType Leaf)) {
    throw "The MiniPC Runtime Host handoff was not created."
}

$document = Get-Content -LiteralPath $destination -Raw | ConvertFrom-Json
if ($document.formatVersion -ne 1 -or
    [string]::IsNullOrWhiteSpace([string]$document.runtimeHostId)) {
    throw "The MiniPC Runtime Host handoff failed local verification."
}

Write-Host
Write-Host "HASE MiniPC Runtime Host onboarding handoff succeeded."
Write-Host "Installation audit          : Ready"
Write-Host "Handoff format              : Ready"
Write-Host "Runtime Host identity       : Withheld"
Write-Host "Installed Runtime Host state: Preserved"
Write-Host "Sensitive deployment values : Withheld"
