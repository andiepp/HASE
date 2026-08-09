[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $ProfilePath
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$toolDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$packageDirectory = Split-Path -Parent $toolDirectory
$python = Join-Path $packageDirectory ".venv\Scripts\python.exe"

if (-not (Test-Path -LiteralPath $python -PathType Leaf))
{
    Write-Error "Python physical Property-write validation failed: python-environment-unavailable."
    exit 1
}

& $python -m hase._physical_property_write_validation $ProfilePath
if ($LASTEXITCODE -ne 0)
{
    exit 1
}
