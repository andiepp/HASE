[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$toolDirectory =
    Split-Path -Parent $MyInvocation.MyCommand.Path
$packageDirectory =
    Split-Path -Parent $toolDirectory
$virtualEnvironmentDirectory =
    Join-Path $packageDirectory ".venv"
$virtualEnvironmentPython =
    Join-Path $virtualEnvironmentDirectory "Scripts\python.exe"
$requirementsPath =
    Join-Path $packageDirectory "requirements-development.txt"

$pythonCommands =
    @(
        Get-Command `
            python `
            -CommandType Application `
            -ErrorAction SilentlyContinue
    )

if ($pythonCommands.Count -eq 0)
{
    throw "CPython was not found through the python command."
}

$pythonCandidatePaths =
    @(
        $pythonCommands `
        | ForEach-Object { $_.Source } `
        | Select-Object -Unique
    )

$selectedPythonPath =
    $null
$selectedPythonFacts =
    $null

foreach ($pythonCandidatePath in $pythonCandidatePaths)
{
    try
    {
        $pythonFactsJson =
            & $pythonCandidatePath -c `
                "import json, platform, struct, sys; print(json.dumps({'implementation': platform.python_implementation(), 'major': sys.version_info.major, 'minor': sys.version_info.minor, 'bits': struct.calcsize('P') * 8}))" `
                2>$null

        if ($LASTEXITCODE -ne 0)
        {
            continue
        }

        $pythonFacts =
            $pythonFactsJson | ConvertFrom-Json

        if (
            $pythonFacts.implementation -eq "CPython" `
            -and $pythonFacts.major -eq 3 `
            -and $pythonFacts.minor -ge 12 `
            -and $pythonFacts.minor -le 13 `
            -and $pythonFacts.bits -eq 64)
        {
            $selectedPythonPath =
                $pythonCandidatePath
            $selectedPythonFacts =
                $pythonFacts
            break
        }
    }
    catch
    {
        continue
    }
}

if ($null -eq $selectedPythonPath)
{
    throw "No compatible 64-bit CPython 3.12 or 3.13 interpreter was found."
}

Write-Host ("Selected CPython : {0}" -f $selectedPythonPath)
$selectedVersionText =
    "Selected version : {0}.{1} ({2}-bit)" -f `
    $selectedPythonFacts.major, `
    $selectedPythonFacts.minor, `
    $selectedPythonFacts.bits
Write-Host $selectedVersionText

if (-not (Test-Path -LiteralPath $virtualEnvironmentPython -PathType Leaf))
{
    Write-Host "Creating repository-local Python virtual environment."
    & $selectedPythonPath -m venv $virtualEnvironmentDirectory

    if ($LASTEXITCODE -ne 0)
    {
        throw "Python virtual-environment creation failed."
    }
}

Write-Host "Installing the recorded development dependency set."
& $virtualEnvironmentPython -m pip install `
    --require-virtualenv `
    --disable-pip-version-check `
    --requirement $requirementsPath

if ($LASTEXITCODE -ne 0)
{
    throw "Python development dependency installation failed."
}

Write-Host "Installing hase-client as an editable package."
& $virtualEnvironmentPython -m pip install `
    --require-virtualenv `
    --disable-pip-version-check `
    --no-build-isolation `
    --no-deps `
    --editable $packageDirectory

if ($LASTEXITCODE -ne 0)
{
    throw "Editable hase-client installation failed."
}

Write-Host
Write-Host "HASE Python development environment"
Write-Host "==================================="
& $virtualEnvironmentPython -c `
    "import importlib.metadata as metadata, platform; print(f'Python          : {platform.python_version()}'); packages = ('grpcio', 'protobuf', 'grpcio-tools', 'pytest', 'hase-client'); [print(f'{package:15}: {metadata.version(package)}') for package in packages]"

if ($LASTEXITCODE -ne 0)
{
    throw "Python development environment verification failed."
}
