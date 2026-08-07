[CmdletBinding()]
param(
    [string] $OutputRoot,
    [switch] $Quiet
)

$ErrorActionPreference = "Stop"

$toolDirectory =
    Split-Path -Parent $MyInvocation.MyCommand.Path
$packageDirectory =
    Split-Path -Parent $toolDirectory
$pythonDirectory =
    Split-Path -Parent $packageDirectory
$repositoryDirectory =
    Split-Path -Parent $pythonDirectory
$contractDirectory =
    Join-Path `
        $repositoryDirectory `
        "src\Hase.Runtime.Remote.Grpc.Contracts\Protos"
$contractFileName =
    "runtime_host_remote_api_v1.proto"
$contractPath =
    Join-Path $contractDirectory $contractFileName
$virtualEnvironmentPython =
    Join-Path $packageDirectory ".venv\Scripts\python.exe"

if (-not (Test-Path -LiteralPath $virtualEnvironmentPython -PathType Leaf))
{
    throw "The local Python environment is absent. Run Initialize-HasePythonDevelopment.ps1 first."
}

if (-not (Test-Path -LiteralPath $contractPath -PathType Leaf))
{
    throw "The authoritative Runtime Host protobuf contract was not found."
}

if ([string]::IsNullOrWhiteSpace($OutputRoot))
{
    $OutputRoot =
        Join-Path $packageDirectory "src"
}

$resolvedOutputRoot =
    [System.IO.Path]::GetFullPath($OutputRoot)
$generatedDirectory =
    Join-Path $resolvedOutputRoot "hase\_generated"

[void](
    New-Item `
        -ItemType Directory `
        -Path $generatedDirectory `
        -Force
)

$protoPathArgument =
    "--proto_path=hase/_generated={0}" -f $contractDirectory
$pythonOutputArgument =
    "--python_out={0}" -f $resolvedOutputRoot
$grpcOutputArgument =
    "--grpc_python_out={0}" -f $resolvedOutputRoot
$virtualContractPath =
    "hase/_generated/{0}" -f $contractFileName

& $virtualEnvironmentPython `
    -m grpc_tools.protoc `
    $protoPathArgument `
    $pythonOutputArgument `
    $grpcOutputArgument `
    $virtualContractPath

if ($LASTEXITCODE -ne 0)
{
    throw "HASE Python contract generation failed."
}

$expectedGeneratedNames =
    @(
        "runtime_host_remote_api_v1_pb2.py",
        "runtime_host_remote_api_v1_pb2_grpc.py"
    )
$actualGeneratedNames =
    @(
        Get-ChildItem `
            -LiteralPath $generatedDirectory `
            -File `
        | Where-Object { $_.Name -ne "__init__.py" } `
        | Select-Object -ExpandProperty Name `
        | Sort-Object
    )

$expectedSortedNames =
    @($expectedGeneratedNames | Sort-Object)

if (
    $actualGeneratedNames.Count -ne $expectedSortedNames.Count `
    -or (Compare-Object $expectedSortedNames $actualGeneratedNames))
{
    throw "Contract generation produced an unexpected file set."
}

if (-not $Quiet)
{
    Write-Host "HASE Python contract generation succeeded."
    Write-Host ("Authoritative contract : {0}" -f $contractPath)
    Write-Host ("Generated directory    : {0}" -f $generatedDirectory)
}
