[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $TargetRegistryPath,

    [Parameter(Mandatory = $true)]
    [string] $InstallationDirectory
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$toolDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$packageDirectory = Split-Path -Parent $toolDirectory
$repositoryRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $packageDirectory "..\.."))
$virtualEnvironmentPython = Join-Path `
    $packageDirectory `
    ".venv\Scripts\python.exe"

if (-not (Test-Path -LiteralPath $virtualEnvironmentPython -PathType Leaf))
{
    Write-Error "Laptop Python target readiness failed: python-environment-unavailable."
    exit 1
}

& $virtualEnvironmentPython `
    -m hase._client_automation_target_readiness `
    $TargetRegistryPath `
    $repositoryRoot `
    $InstallationDirectory

if ($LASTEXITCODE -ne 0)
{
    exit 1
}
