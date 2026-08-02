[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$DestinationPath
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if (-not [System.IO.Path]::IsPathRooted($DestinationPath) -or
    $DestinationPath -match '^[A-Za-z]:[^\\/]') {
    throw "The onboarding handoff destination path must be fully qualified."
}

$destination = [System.IO.Path]::GetFullPath($DestinationPath)
$repositoryRoot = [System.IO.Path]::GetFullPath(
    (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))
$auditProject = Join-Path $repositoryRoot `
    "src\Hase.DesktopHost.OnboardingAudit\Hase.DesktopHost.OnboardingAudit.csproj"
$installationDirectory = Join-Path $env:LOCALAPPDATA "HASE\RuntimeHost"

& dotnet run --project $auditProject -c Release --no-build -- `
    export $installationDirectory $destination

if ($LASTEXITCODE -ne 0) {
    throw "The Runtime Host onboarding handoff was not created."
}
