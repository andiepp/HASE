[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$toolDirectory =
    Split-Path -Parent $MyInvocation.MyCommand.Path
$packageDirectory =
    Split-Path -Parent $toolDirectory
$virtualEnvironmentPython =
    Join-Path $packageDirectory ".venv\Scripts\python.exe"

if (-not (Test-Path -LiteralPath $virtualEnvironmentPython -PathType Leaf))
{
    throw "The local Python environment is absent. Run Initialize-HasePythonDevelopment.ps1 first."
}

Push-Location $packageDirectory

try
{
    & $virtualEnvironmentPython -m pytest

    if ($LASTEXITCODE -ne 0)
    {
        throw "HASE Python tests failed."
    }
}
finally
{
    Pop-Location
}
