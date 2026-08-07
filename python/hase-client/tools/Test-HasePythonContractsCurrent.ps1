[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$toolDirectory =
    Split-Path -Parent $MyInvocation.MyCommand.Path
$packageDirectory =
    Split-Path -Parent $toolDirectory
$generationScript =
    Join-Path $toolDirectory "Generate-HasePythonContracts.ps1"
$committedGeneratedDirectory =
    Join-Path $packageDirectory "src\hase\_generated"
$temporaryRoot =
    Join-Path `
        ([System.IO.Path]::GetTempPath()) `
        ("hase-python-contract-{0}" -f [guid]::NewGuid().ToString("N"))
$generatedFileNames =
    @(
        "runtime_host_remote_api_v1_pb2.py",
        "runtime_host_remote_api_v1_pb2_grpc.py"
    )

function Test-EqualFileBytes
{
    param(
        [Parameter(Mandatory = $true)]
        [string] $ExpectedPath,
        [Parameter(Mandatory = $true)]
        [string] $ActualPath
    )

    $expectedBytes =
        [System.IO.File]::ReadAllBytes($ExpectedPath)
    $actualBytes =
        [System.IO.File]::ReadAllBytes($ActualPath)

    if ($expectedBytes.Length -ne $actualBytes.Length)
    {
        return $false
    }

    for ($index = 0; $index -lt $expectedBytes.Length; $index++)
    {
        if ($expectedBytes[$index] -ne $actualBytes[$index])
        {
            return $false
        }
    }

    return $true
}

try
{
    [void](New-Item -ItemType Directory -Path $temporaryRoot)

    & $generationScript -OutputRoot $temporaryRoot -Quiet

    $temporaryGeneratedDirectory =
        Join-Path $temporaryRoot "hase\_generated"

    foreach ($generatedFileName in $generatedFileNames)
    {
        $committedPath =
            Join-Path $committedGeneratedDirectory $generatedFileName
        $temporaryPath =
            Join-Path $temporaryGeneratedDirectory $generatedFileName

        if (-not (Test-Path -LiteralPath $committedPath -PathType Leaf))
        {
            throw "A committed generated contract file is missing."
        }

        if (-not (Test-Path -LiteralPath $temporaryPath -PathType Leaf))
        {
            throw "A regenerated contract file is missing."
        }

        if (-not (Test-EqualFileBytes $committedPath $temporaryPath))
        {
            throw "Generated Python contracts are stale. Run Generate-HasePythonContracts.ps1."
        }
    }

    Write-Host "Committed Python contracts match fresh generation exactly."
}
finally
{
    if (Test-Path -LiteralPath $temporaryRoot)
    {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}

