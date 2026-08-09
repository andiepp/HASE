[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $ManifestPath,
    [Parameter(Mandatory = $true)] [string] $RollbackEvidencePath
)
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
try
{
    foreach ($path in @($ManifestPath, $RollbackEvidencePath))
    { if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "evidence" } }
    $manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
    $rollback = Get-Content -LiteralPath $RollbackEvidencePath -Raw | ConvertFrom-Json
    if ($manifest.schemaVersion -ne 1 `
        -or $manifest.purpose -cne "hase-minipc-python-client-authority" `
        -or $manifest.thumbprint -cne $rollback.thumbprint `
        -or $manifest.certificateSha256 -cne $rollback.certificateSha256)
    { throw "evidence" }
    $thumbprint = [string]$manifest.thumbprint
    $personal = @(Get-ChildItem Cert:\CurrentUser\My | Where-Object {
        $_.Thumbprint -eq $thumbprint })
    $trusted = @(Get-ChildItem Cert:\CurrentUser\Root | Where-Object {
        $_.Thumbprint -eq $thumbprint })
    if ($personal.Count -ne 1 -or $trusted.Count -ne 1 `
        -or -not $personal[0].HasPrivateKey -or $trusted[0].HasPrivateKey)
    { throw "authority" }
    $hasher = [System.Security.Cryptography.SHA256]::Create()
    try { $sha = $hasher.ComputeHash($personal[0].RawData) }
    finally { $hasher.Dispose() }
    $shaHex = [System.BitConverter]::ToString($sha).Replace("-", "").ToLowerInvariant()
    if ($shaHex `
        -cne [string]$manifest.certificateSha256) { throw "authority" }
    Remove-Item -LiteralPath ("Cert:\CurrentUser\Root\" + $thumbprint) -Force
    Remove-Item -LiteralPath ("Cert:\CurrentUser\My\" + $thumbprint) -Force
    Remove-Item -LiteralPath $ManifestPath -Force
    Remove-Item -LiteralPath $RollbackEvidencePath -Force
    Write-Host "Trusted root removed      : True"
    Write-Host "Private authority removed: True"
    Write-Host "Evidence removed         : True"
    Write-Host "Authority removal complete: True"
}
catch
{
    Write-Error "MiniPC Python client authority removal failed."
    exit 1
}
