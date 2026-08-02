[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PrivateNetworkConfigurationPath,
    [string]$CompactVendorId = "0x2341",
    [string]$CompactProductId = "0x0043"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if (-not [System.IO.Path]::IsPathRooted($PrivateNetworkConfigurationPath) -or
    $PrivateNetworkConfigurationPath -match '^[A-Za-z]:[^\\/]') {
    throw "The private-network configuration path must be fully qualified."
}

$repositoryRoot = [System.IO.Path]::GetFullPath(
    (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))
$projectPath = Join-Path $repositoryRoot `
    "src\Hase.DesktopHost.Preflight\Hase.DesktopHost.Preflight.csproj"

& dotnet run --project $projectPath -c Release --no-build -- `
    $repositoryRoot `
    ([System.IO.Path]::GetFullPath($PrivateNetworkConfigurationPath)) `
    $CompactVendorId `
    $CompactProductId

if ($LASTEXITCODE -ne 0) {
    throw "The second-PC Runtime Host preflight reported one or more blockers."
}
