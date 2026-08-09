[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $ProfilePath
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$toolDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$packageDirectory = Split-Path -Parent $toolDirectory
$virtualEnvironmentPython =
    Join-Path $packageDirectory ".venv\Scripts\python.exe"

if (-not (Test-Path -LiteralPath $virtualEnvironmentPython -PathType Leaf))
{
    Write-Error "Python physical Property validation failed: python-environment-unavailable."
    exit 1
}

& $virtualEnvironmentPython `
    -m hase._physical_authoritative_property_validation `
    $ProfilePath

if ($LASTEXITCODE -ne 0)
{
    exit 1
}
