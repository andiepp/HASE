[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $AuthorizationPolicyPath,
    [Parameter(Mandatory = $true)][string] $RollbackPath
)
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$policyReady =
    Test-Path `
        -LiteralPath $AuthorizationPolicyPath `
        -PathType Leaf

$rollbackAvailable =
    -not (Test-Path -LiteralPath $RollbackPath)

if (-not $policyReady) {
    Write-Error "Python command authorization failed: inputs-invalid."
    exit 1
}

if (-not $rollbackAvailable) {
    Write-Error "Python command authorization failed: inputs-invalid."
    exit 1
}
$hash = (Get-FileHash -LiteralPath $AuthorizationPolicyPath -Algorithm SHA256).Hash.ToLowerInvariant()
$root = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
& dotnet run --project (Join-Path $root "src\Hase.Python.CredentialProvisioning.Operator") `
    -c Release --no-build -- authorize-command-execution `
    --authorization-policy $AuthorizationPolicyPath `
    --expected-authorization-policy-sha256 $hash `
    --rollback $RollbackPath
exit $LASTEXITCODE
