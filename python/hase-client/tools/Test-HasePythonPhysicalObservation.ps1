[CmdletBinding()]
param([Parameter(Mandatory = $true)][string] $ProfilePath)
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$python = Join-Path $root ".venv\Scripts\python.exe"
if (-not (Test-Path -LiteralPath $python -PathType Leaf)) { exit 1 }
& $python -m hase._physical_observation_validation $ProfilePath
exit $LASTEXITCODE
