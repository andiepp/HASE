[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $AuthorizationPolicyPath,

    [Parameter(Mandatory = $true)]
    [string] $ApplicationProfilePath,

    [Parameter(Mandatory = $true)]
    [string] $PolicyRollbackPath,

    [Parameter(Mandatory = $true)]
    [string] $ProfileRollbackPath
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Test-AbsolutePath([string] $Path)
{
    return (
        $Path -match '^[A-Za-z]:[\\/]' -or
        $Path -match '^\\\\[^\\/]+[\\/][^\\/]+(?:[\\/]|$)'
    )
}

if (
    -not (Test-AbsolutePath $AuthorizationPolicyPath) -or
    -not (Test-AbsolutePath $ApplicationProfilePath) -or
    -not (Test-AbsolutePath $PolicyRollbackPath) -or
    -not (Test-AbsolutePath $ProfileRollbackPath) -or
    -not (Test-Path -LiteralPath $AuthorizationPolicyPath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $ApplicationProfilePath -PathType Leaf) -or
    (Test-Path -LiteralPath $PolicyRollbackPath) -or
    (Test-Path -LiteralPath $ProfileRollbackPath)
)
{
    Write-Error "Python Property-write authorization failed: authorization-target-invalid."
    exit 1
}

$repositoryDirectory =
    Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
$expectedHash =
    (Get-FileHash -LiteralPath $AuthorizationPolicyPath -Algorithm SHA256).Hash.ToLowerInvariant()
$expectedProfileHash =
    (Get-FileHash -LiteralPath $ApplicationProfilePath -Algorithm SHA256).Hash.ToLowerInvariant()

& dotnet run `
    --project (Join-Path $repositoryDirectory "src\Hase.Python.CredentialProvisioning.Operator") `
    -c Release `
    --no-build `
    -- `
    authorize-property-write `
    --authorization-policy $AuthorizationPolicyPath `
    --expected-authorization-policy-sha256 $expectedHash `
    --application-profile $ApplicationProfilePath `
    --expected-application-profile-sha256 $expectedProfileHash `
    --policy-rollback $PolicyRollbackPath `
    --profile-rollback $ProfileRollbackPath

if ($LASTEXITCODE -ne 0)
{
    exit 1
}
