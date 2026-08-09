[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $PackagePath,

    [Parameter(Mandatory = $true)]
    [string] $InstallationDirectory,

    [string] $HashPath
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

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
                [System.IO.Path]::GetFullPath($Path).TrimEnd("\"),
                $Path.TrimEnd("\"),
                [System.StringComparison]::OrdinalIgnoreCase)
    }
    catch
    {
        return $false
    }
}

$toolDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$packageDirectory = Split-Path -Parent $toolDirectory
$launcherSource = Join-Path $toolDirectory "Invoke-HasePythonAutomation.ps1"

if (-not (Test-AbsolutePath -Path $PackagePath) `
    -or -not (Test-Path -LiteralPath $PackagePath -PathType Leaf) `
    -or [System.IO.Path]::GetExtension($PackagePath) -ne ".whl")
{
    Write-Error "HASE automation installation failed: package-path-invalid."
    exit 1
}
if ([string]::IsNullOrWhiteSpace($HashPath))
{
    $HashPath = "{0}.sha256" -f $PackagePath
}
if (-not (Test-AbsolutePath -Path $HashPath) `
    -or -not (Test-Path -LiteralPath $HashPath -PathType Leaf))
{
    Write-Error "HASE automation installation failed: hash-path-invalid."
    exit 1
}
if (-not (Test-AbsolutePath -Path $InstallationDirectory) `
    -or (Test-Path -LiteralPath $InstallationDirectory))
{
    Write-Error "HASE automation installation failed: installation-target-invalid."
    exit 1
}
$repositoryDirectory = [System.IO.Path]::GetFullPath(
    (Join-Path $packageDirectory "..\.."))
$normalizedTarget = [System.IO.Path]::GetFullPath($InstallationDirectory)
if ([string]::Equals(
        $normalizedTarget.TrimEnd("\"),
        $repositoryDirectory.TrimEnd("\"),
        [System.StringComparison]::OrdinalIgnoreCase) `
    -or $normalizedTarget.StartsWith(
        $repositoryDirectory.TrimEnd("\") + "\",
        [System.StringComparison]::OrdinalIgnoreCase))
{
    Write-Error "HASE automation installation failed: installation-target-inside-repository."
    exit 1
}
if (-not (Test-Path -LiteralPath $launcherSource -PathType Leaf))
{
    Write-Error "HASE automation installation failed: launcher-unavailable."
    exit 1
}

$hashRecord = (Get-Content -LiteralPath $HashPath -Raw).Trim()
$hashMatch = [regex]::Match(
    $hashRecord,
    '^(?<hash>[0-9a-fA-F]{64})  (?<name>[^\\/]+)$')
if (-not $hashMatch.Success `
    -or $hashMatch.Groups["name"].Value -cne (Split-Path -Leaf $PackagePath))
{
    Write-Error "HASE automation installation failed: hash-record-invalid."
    exit 1
}
$packageHash = (Get-FileHash -LiteralPath $PackagePath -Algorithm SHA256).Hash
if ($packageHash -ine $hashMatch.Groups["hash"].Value)
{
    Write-Error "HASE automation installation failed: package-hash-mismatch."
    exit 1
}

$pythonCommands = @(
    Get-Command python -CommandType Application -ErrorAction SilentlyContinue
)
$selectedPython = $null
foreach ($command in $pythonCommands)
{
    try
    {
        $facts = & $command.Source -c `
            "import json, platform, struct, sys; print(json.dumps({'implementation': platform.python_implementation(), 'major': sys.version_info.major, 'minor': sys.version_info.minor, 'bits': struct.calcsize('P') * 8}))" `
            2>$null | ConvertFrom-Json
        if ($LASTEXITCODE -eq 0 `
            -and $facts.implementation -eq "CPython" `
            -and $facts.major -eq 3 `
            -and $facts.minor -ge 12 `
            -and $facts.minor -le 13 `
            -and $facts.bits -eq 64)
        {
            $selectedPython = $command.Source
            break
        }
    }
    catch
    {
        continue
    }
}
if ($null -eq $selectedPython)
{
    Write-Error "HASE automation installation failed: python-unavailable."
    exit 1
}

$created = $false
try
{
    [System.IO.Directory]::CreateDirectory($InstallationDirectory) | Out-Null
    $created = $true
    $environmentDirectory = Join-Path $InstallationDirectory ".venv"
    & $selectedPython -m venv $environmentDirectory
    if ($LASTEXITCODE -ne 0)
    {
        throw "environment-creation-failed"
    }

    $automationPython = Join-Path $environmentDirectory "Scripts\python.exe"
    & $automationPython -m pip install `
        --require-virtualenv `
        --disable-pip-version-check `
        --quiet `
        $PackagePath
    if ($LASTEXITCODE -ne 0)
    {
        throw "package-installation-failed"
    }

    Push-Location $InstallationDirectory
    try
    {
        $previousPythonPath = $env:PYTHONPATH
        $env:PYTHONPATH = $null
        & $automationPython -m hase._installed_package_validation
        if ($LASTEXITCODE -ne 0)
        {
            throw "installed-package-validation-failed"
        }
    }
    finally
    {
        $env:PYTHONPATH = $previousPythonPath
        Pop-Location
    }

    $installedFacts = & $automationPython -c `
        "import importlib.metadata as m, json, platform; print(json.dumps({'packageVersion': m.version('hase-client'), 'pythonVersion': platform.python_version()}))" `
        | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0)
    {
        throw "installed-facts-unavailable"
    }

    Copy-Item -LiteralPath $launcherSource `
        -Destination (Join-Path $InstallationDirectory "Invoke-HasePythonAutomation.ps1")
    $manifest = [ordered]@{
        schemaVersion = 1
        packageName = "hase-client"
        packageVersion = $installedFacts.packageVersion
        packageSha256 = $packageHash.ToLowerInvariant()
        pythonVersion = $installedFacts.pythonVersion
        installedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    }
    $manifestJson = $manifest | ConvertTo-Json
    $utf8WithoutBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText(
        (Join-Path $InstallationDirectory "installation-manifest.json"),
        $manifestJson,
        $utf8WithoutBom)
}
catch
{
    if ($created -and (Test-Path -LiteralPath $InstallationDirectory))
    {
        Remove-Item -LiteralPath $InstallationDirectory -Recurse -Force
    }
    $knownFailures = @(
        "environment-creation-failed",
        "package-installation-failed",
        "installed-package-validation-failed",
        "installed-facts-unavailable"
    )
    $failureCode = $_.Exception.Message
    if ($failureCode -notin $knownFailures)
    {
        $failureCode = "unexpected-failure"
    }
    Write-Error ("HASE automation installation failed: {0}." -f $failureCode)
    exit 1
}

Write-Host "Package hash verified    : True"
Write-Host "Private environment ready: True"
Write-Host "Installed package valid  : True"
Write-Host "Launcher installed       : True"
Write-Host "Manifest recorded        : True"
Write-Host "Installation succeeded   : True"
