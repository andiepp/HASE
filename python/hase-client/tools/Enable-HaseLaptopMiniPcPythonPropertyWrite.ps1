[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $AuthorizationPolicyPath,
    [Parameter(Mandatory = $true)][string] $RollbackPath
)
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if (-not (Test-Path -LiteralPath $AuthorizationPolicyPath -PathType Leaf)) {
    Write-Error "Laptop MiniPC Property-write authorization failed: inputs-invalid."
    exit 1
}
if (Test-Path -LiteralPath $RollbackPath) {
    Write-Error "Laptop MiniPC Property-write authorization failed: inputs-invalid."
    exit 1
}

$hash = (
    Get-FileHash `
        -LiteralPath $AuthorizationPolicyPath `
        -Algorithm SHA256
).Hash.ToLowerInvariant()

$root = Split-Path -Parent (
    Split-Path -Parent (
        Split-Path -Parent $PSScriptRoot
    )
)

& dotnet run `
    --project (Join-Path $root "src\Hase.Python.CredentialProvisioning.Operator") `
    -c Release `
    --no-build `
    -- authorize-laptop-minipc-property-write `
    --authorization-policy $AuthorizationPolicyPath `
    --expected-authorization-policy-sha256 $hash `
    --rollback $RollbackPath

exit $LASTEXITCODE
