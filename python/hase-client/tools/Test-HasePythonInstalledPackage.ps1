[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $PackagePath,

    [Parameter(Mandatory = $true)]
    [string] $ProfilePath
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$toolDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$packageDirectory = Split-Path -Parent $toolDirectory
$developmentPython = Join-Path $packageDirectory ".venv\Scripts\python.exe"

function Test-AbsolutePath
{
    param([string] $Path)

    if ([string]::IsNullOrWhiteSpace($Path))
    {
        return $false
    }

    try
    {
        return [System.IO.Path]::IsPathRooted($Path) `
            -and [string]::Equals(
                [System.IO.Path]::GetFullPath($Path),
                $Path,
                [System.StringComparison]::OrdinalIgnoreCase)
    }
    catch
    {
        return $false
    }
}

if (-not (Test-Path -LiteralPath $developmentPython -PathType Leaf))
{
    Write-Error "Installed-package validation failed: python-environment-unavailable."
    exit 1
}
if (-not (Test-AbsolutePath -Path $PackagePath) `
    -or -not (Test-Path -LiteralPath $PackagePath -PathType Leaf) `
    -or [System.IO.Path]::GetExtension($PackagePath) -ne ".whl")
{
    Write-Error "Installed-package validation failed: package-path-invalid."
    exit 1
}
if (-not (Test-AbsolutePath -Path $ProfilePath) `
    -or -not (Test-Path -LiteralPath $ProfilePath -PathType Leaf))
{
    Write-Error "Installed-package validation failed: profile-path-invalid."
    exit 1
}

$validationRoot = Join-Path $packageDirectory "package-validation"
if (Test-Path -LiteralPath $validationRoot)
{
    Write-Error "Installed-package validation failed: validation-environment-exists."
    exit 1
}

try
{
    & $developmentPython -m venv $validationRoot
    if ($LASTEXITCODE -ne 0)
    {
        throw "validation-environment-creation-failed"
    }

    $validationPython = Join-Path $validationRoot "Scripts\python.exe"
    & $validationPython -m pip install `
        --require-virtualenv `
        --disable-pip-version-check `
        $PackagePath
    if ($LASTEXITCODE -ne 0)
    {
        throw "wheel-installation-failed"
    }

    Push-Location $validationRoot
    try
    {
        $previousPythonPath = $env:PYTHONPATH
        $env:PYTHONPATH = $null
        & $validationPython -m hase._installed_package_validation
        if ($LASTEXITCODE -ne 0)
        {
            throw "installed-surface-validation-failed"
        }

        & $validationPython -m hase._physical_snapshot_validation $ProfilePath
        if ($LASTEXITCODE -ne 0)
        {
            throw "installed-physical-snapshot-failed"
        }
    }
    finally
    {
        $env:PYTHONPATH = $previousPythonPath
        Pop-Location
    }
}
catch
{
    Write-Error ("Installed-package validation failed: {0}." -f $_.Exception.Message)
    exit 1
}
finally
{
    if (Test-Path -LiteralPath $validationRoot)
    {
        Remove-Item -LiteralPath $validationRoot -Recurse -Force
    }
}

Write-Host "Fresh environment removed: True"
