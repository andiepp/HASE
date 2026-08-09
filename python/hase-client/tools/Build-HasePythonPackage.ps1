[CmdletBinding()]
param(
    [string] $OutputDirectory
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$toolDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$packageDirectory = Split-Path -Parent $toolDirectory
$virtualEnvironmentPython = Join-Path $packageDirectory ".venv\Scripts\python.exe"

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

if (-not (Test-Path -LiteralPath $virtualEnvironmentPython -PathType Leaf))
{
    Write-Error "Python package build failed: python-environment-unavailable."
    exit 1
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory))
{
    $OutputDirectory = Join-Path $packageDirectory "dist"
}
elseif (-not (Test-AbsolutePath -Path $OutputDirectory))
{
    Write-Error "Python package build failed: output-directory-not-absolute."
    exit 1
}

[System.IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null

$existingWheels = @(
    Get-ChildItem -LiteralPath $OutputDirectory -Filter "hase_client-*.whl" -File
)
if ($existingWheels.Count -ne 0)
{
    Write-Error "Python package build failed: output-already-contains-wheel."
    exit 1
}

& $virtualEnvironmentPython -m pip wheel `
    --require-virtualenv `
    --disable-pip-version-check `
    --no-build-isolation `
    --no-deps `
    --wheel-dir $OutputDirectory `
    $packageDirectory

if ($LASTEXITCODE -ne 0)
{
    Write-Error "Python package build failed: wheel-build-failed."
    exit 1
}

$wheels = @(
    Get-ChildItem -LiteralPath $OutputDirectory -Filter "hase_client-*.whl" -File
)
if ($wheels.Count -ne 1)
{
    Write-Error "Python package build failed: unexpected-wheel-count."
    exit 1
}

$wheel = $wheels[0]
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($wheel.FullName)
try
{
    $contents = @($archive.Entries.FullName | Sort-Object)
}
finally
{
    $archive.Dispose()
}

$requiredEntries = @(
    "hase/__init__.py",
    "hase/_generated/runtime_host_remote_api_v1_pb2.py",
    "hase/_generated/runtime_host_remote_api_v1_pb2_grpc.py"
)
foreach ($requiredEntry in $requiredEntries)
{
    if ($contents -notcontains $requiredEntry)
    {
        Write-Error "Python package build failed: required-content-missing."
        exit 1
    }
}

$forbiddenPattern =
    '(?i)(^|/)(\.venv|\.git|__pycache__|credentials?|profiles?|rollback|cache)(/|$)|\.(pem|key|pfx|p12|cer|crt)$'
if (@($contents | Where-Object { $_ -match $forbiddenPattern }).Count -ne 0)
{
    Write-Error "Python package build failed: forbidden-content-detected."
    exit 1
}

$contentsPath = "{0}.contents.txt" -f $wheel.FullName
$hashPath = "{0}.sha256" -f $wheel.FullName
$utf8WithoutBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllLines(
    $contentsPath,
    [string[]] $contents,
    $utf8WithoutBom)
$hash = (Get-FileHash -LiteralPath $wheel.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
("{0}  {1}" -f $hash, $wheel.Name) |
    Set-Content -LiteralPath $hashPath -Encoding ascii -NoNewline

Write-Host ("Wheel                   : {0}" -f $wheel.Name)
Write-Host ("Package content records : {0}" -f $contents.Count)
Write-Host ("SHA-256                 : {0}" -f $hash)
Write-Host "Sensitive content absent: True"
