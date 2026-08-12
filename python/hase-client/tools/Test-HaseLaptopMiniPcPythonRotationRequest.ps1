[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $RequestPath
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

try
{
    if ($env:OS -ne "Windows_NT") { throw "platform" }
    if ([string]::IsNullOrWhiteSpace($RequestPath) `
        -or -not ($RequestPath -match '^[A-Za-z]:[\\/]')) { throw "path" }
    $path = [IO.Path]::GetFullPath($RequestPath)
    if (-not (Test-Path -LiteralPath $path -PathType Leaf) `
        -or (Get-Item -LiteralPath $path).Attributes -band
            [IO.FileAttributes]::ReparsePoint) { throw "input" }
    if (-not (Get-Acl -LiteralPath $path).AreAccessRulesProtected)
    { throw "acl" }
    $request = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    $names = @($request.PSObject.Properties.Name | Sort-Object)
    $expectedNames = @(
        "certificateSha256", "createdUtc", "expectedCurrentCredentialId",
        "expectedGrants", "principalId", "privateKeySha256", "profileSha256",
        "purpose", "repositoryHead", "schemaVersion", "targetId",
        "trustedServerCertificateSha256") | Sort-Object
    if (@(Compare-Object $names $expectedNames).Count -ne 0 `
        -or [int]$request.schemaVersion -ne 1 `
        -or [string]$request.purpose -cne
            "hase-laptop-minipc-python-cross-computer-rotation-request" `
        -or [string]$request.targetId -cne "minipc-runtime-host" `
        -or [string]$request.principalId -cne "hase-laptop-python-minipc" `
        -or [string]$request.repositoryHead -notmatch '^[0-9a-f]{40}$' `
        -or [string]$request.expectedCurrentCredentialId -notmatch
            '^x509-sha256:[0-9a-f]{64}$') { throw "document" }
    foreach ($name in @("certificateSha256", "privateKeySha256",
            "profileSha256", "trustedServerCertificateSha256"))
    {
        if ([string]$request.$name -notmatch '^[0-9a-f]{64}$')
        { throw "hash" }
    }
    $grants = @($request.expectedGrants | Sort-Object -Unique)
    $expected = @("command.execute", "observation.subscribe",
        "property.authoritative.read", "property.write",
        "runtime-host.snapshot.read") | Sort-Object
    if (@(Compare-Object $grants $expected).Count -ne 0) { throw "grants" }
    $created = [DateTimeOffset]::ParseExact([string]$request.createdUtc, "O",
        [Globalization.CultureInfo]::InvariantCulture)
    if ($created.Offset -ne [TimeSpan]::Zero) { throw "time" }

    Write-Host "Strict request shape valid      : True"
    Write-Host "Credential identity valid       : True"
    Write-Host "Source revisions valid          : True"
    Write-Host "Five exact grants valid         : True"
    Write-Host "Protected custody valid         : True"
    Write-Host "Private key material present    : False"
    Write-Host "Profile content present         : False"
    Write-Host "Rotation request valid          : True"
}
catch
{
    Write-Error "Laptop MiniPC Python rotation request validation failed."
    exit 1
}
