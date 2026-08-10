[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $RuntimeArchivePath,

    [Parameter(Mandatory = $true)]
    [string] $InstallationDirectory
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$expectedHash = "9877d0d24f7978407bde1b50ab1023b0f5c67ff6c9816b834e5258db1a636249"
$stagePath = $null
$runtimePublished = $false
$environmentCreated = $false

function Resolve-AbsolutePath
{
    param([string] $Value)
    if ([string]::IsNullOrWhiteSpace($Value) `
        -or $Value -ne $Value.Trim() `
        -or -not ($Value -match '^[A-Za-z]:[\\/]'))
    {
        throw "path-invalid"
    }
    return [System.IO.Path]::GetFullPath($Value)
}

try
{
    if ($env:OS -ne "Windows_NT") { throw "platform" }

    $archivePath = Resolve-AbsolutePath $RuntimeArchivePath
    $runtimePath = Resolve-AbsolutePath $InstallationDirectory
    $toolDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
    $packageDirectory = Split-Path -Parent $toolDirectory
    $repositoryRoot = [System.IO.Path]::GetFullPath(
        (Split-Path -Parent (Split-Path -Parent $packageDirectory)))
    $environmentPath = Join-Path $packageDirectory ".venv"
    $requirementsPath = Join-Path $packageDirectory "requirements-development.txt"
    $haseRoot = Join-Path $env:LOCALAPPDATA "HASE"

    if (-not (Test-Path -LiteralPath $archivePath -PathType Leaf) `
        -or [System.IO.Path]::GetExtension($archivePath) -ine ".zip" `
        -or (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expectedHash)
    {
        throw "runtime-archive"
    }
    $hasePrefix = [System.IO.Path]::GetFullPath($haseRoot).TrimEnd("\") + "\"
    if (-not $runtimePath.StartsWith(
            $hasePrefix, [System.StringComparison]::OrdinalIgnoreCase) `
        -or (Test-Path -LiteralPath $runtimePath) `
        -or -not (Test-Path -LiteralPath (Split-Path -Parent $runtimePath) -PathType Container) `
        -or (Test-Path -LiteralPath $environmentPath))
    {
        throw "publication-target"
    }
    if (-not (Test-Path -LiteralPath $requirementsPath -PathType Leaf))
    {
        throw "requirements"
    }
    if (@(& git -C $repositoryRoot status --porcelain).Count -ne 0)
    {
        throw "repository"
    }
    $head = (& git -C $repositoryRoot rev-parse HEAD).Trim()
    $origin = (& git -C $repositoryRoot rev-parse origin/main).Trim()
    if ($LASTEXITCODE -ne 0 -or $head -ne $origin) { throw "repository" }

    $stagePath = $runtimePath + ".stage-" + [guid]::NewGuid().ToString("N")
    Expand-Archive -LiteralPath $archivePath -DestinationPath $stagePath
    $stagePython = Join-Path $stagePath "python.exe"
    if (-not (Test-Path -LiteralPath $stagePython -PathType Leaf))
    {
        throw "runtime-content"
    }
    $runtimeIdentity = & $stagePython -c `
        "import platform,sys; print(f'{sys.version_info.major}.{sys.version_info.minor}.{sys.version_info.micro}|{platform.architecture()[0]}')"
    if ($LASTEXITCODE -ne 0 -or $runtimeIdentity.Trim() -ne "3.13.1|64bit")
    {
        throw "runtime-identity"
    }

    Move-Item -LiteralPath $stagePath -Destination $runtimePath
    $stagePath = $null
    $runtimePublished = $true
    $runtimePython = Join-Path $runtimePath "python.exe"
    & $runtimePython -m venv $environmentPath
    if ($LASTEXITCODE -ne 0) { throw "environment" }
    $environmentCreated = $true
    $environmentPython = Join-Path $environmentPath "Scripts\python.exe"
    & $environmentPython -m pip install `
        --disable-pip-version-check `
        --requirement $requirementsPath
    if ($LASTEXITCODE -ne 0) { throw "dependencies" }
    & $environmentPython -m pip install `
        --disable-pip-version-check `
        --no-deps `
        --editable $packageDirectory
    if ($LASTEXITCODE -ne 0) { throw "package" }

    $installedIdentity = & $environmentPython -c `
        "import hase,platform,sys; print(f'{sys.version_info.major}.{sys.version_info.minor}.{sys.version_info.micro}|{platform.architecture()[0]}|{hase.__version__}')"
    if ($LASTEXITCODE -ne 0 -or $installedIdentity.Trim() -ne "3.13.1|64bit|0.6.0")
    {
        throw "validation"
    }

    Write-Host "Official archive verified : True"
    Write-Host "Private runtime installed : True"
    Write-Host "Registry installer absent : True"
    Write-Host "PATH mutation absent       : True"
    Write-Host "Local environment created : True"
    Write-Host "Pinned dependencies ready : True"
    Write-Host "HASE package ready         : True"
    Write-Host "MiniPC private Python ready: True"
}
catch
{
    if ($environmentCreated -and (Test-Path -LiteralPath $environmentPath))
    {
        Remove-Item -LiteralPath $environmentPath -Recurse -Force
    }
    if ($runtimePublished -and (Test-Path -LiteralPath $runtimePath))
    {
        Remove-Item -LiteralPath $runtimePath -Recurse -Force
    }
    if ($null -ne $stagePath -and (Test-Path -LiteralPath $stagePath))
    {
        Remove-Item -LiteralPath $stagePath -Recurse -Force
    }
    Write-Error "MiniPC private Python installation failed."
    exit 1
}
