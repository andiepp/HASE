[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $ManifestPath,

    [Parameter(Mandatory = $true)]
    [string] $RollbackEvidencePath
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$subject = "CN=HASE MiniPC Python Client Authority"
$certificate = $null
$rootAdded = $false
$manifestWritten = $false
$rollbackWritten = $false

function Resolve-NewAbsoluteFile
{
    param([string] $Value)
    if ([string]::IsNullOrWhiteSpace($Value) -or $Value -ne $Value.Trim() `
        -or -not ($Value -match '^[A-Za-z]:[\\/]')) { throw "path-invalid" }
    $path = [System.IO.Path]::GetFullPath($Value)
    $parent = Split-Path -Parent $path
    if ((Test-Path -LiteralPath $path) `
        -or -not (Test-Path -LiteralPath $parent -PathType Container))
    { throw "target-invalid" }
    return $path
}

try
{
    if ($env:OS -ne "Windows_NT") { throw "platform" }
    $toolDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
    $packageDirectory = Split-Path -Parent $toolDirectory
    $repositoryRoot = [System.IO.Path]::GetFullPath(
        (Split-Path -Parent (Split-Path -Parent $packageDirectory)))
    if (@(& git -C $repositoryRoot status --porcelain).Count -ne 0)
    { throw "repository" }
    $head = (& git -C $repositoryRoot rev-parse HEAD).Trim()
    $origin = (& git -C $repositoryRoot rev-parse origin/main).Trim()
    if ($LASTEXITCODE -ne 0 -or $head -ne $origin) { throw "repository" }
    if (@(Get-Process -Name "Hase.DesktopHost.App" -ErrorAction SilentlyContinue).Count -ne 0 `
        -or @(Get-Process -Name "Hase.Client.Wpf.App" -ErrorAction SilentlyContinue).Count -ne 0)
    { throw "processes" }

    $manifest = Resolve-NewAbsoluteFile $ManifestPath
    $rollback = Resolve-NewAbsoluteFile $RollbackEvidencePath
    $repositoryPrefix = $repositoryRoot.TrimEnd("\") + "\"
    if ($manifest.StartsWith($repositoryPrefix,
            [System.StringComparison]::OrdinalIgnoreCase) `
        -or $rollback.StartsWith($repositoryPrefix,
            [System.StringComparison]::OrdinalIgnoreCase))
    { throw "paths" }
    if ([string]::Equals($manifest, $rollback,
        [System.StringComparison]::OrdinalIgnoreCase)) { throw "paths" }

    $existing = @(Get-ChildItem Cert:\CurrentUser\My | Where-Object {
        $_.Subject -ceq $subject })
    if ($existing.Count -ne 0) { throw "authority-present" }

    $now = [DateTime]::UtcNow
    $certificate = New-SelfSignedCertificate `
        -Subject $subject `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -KeyAlgorithm RSA `
        -KeyLength 3072 `
        -HashAlgorithm SHA256 `
        -KeyExportPolicy NonExportable `
        -KeyUsage DigitalSignature, CertSign, CRLSign `
        -TextExtension "2.5.29.19={critical}{text}ca=true&pathlength=0" `
        -NotBefore $now.AddMinutes(-5) `
        -NotAfter $now.AddYears(2)

    if ($null -eq $certificate -or -not $certificate.HasPrivateKey)
    { throw "authority-creation" }
    $rootStore = [System.Security.Cryptography.X509Certificates.X509Store]::new(
        [System.Security.Cryptography.X509Certificates.StoreName]::Root,
        [System.Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser)
    try
    {
        $rootStore.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
        $public = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
            $certificate.RawData)
        try { $rootStore.Add($public); $rootAdded = $true }
        finally { $public.Dispose() }
    }
    finally { $rootStore.Dispose() }

    $hasher = [System.Security.Cryptography.SHA256]::Create()
    try { $sha = $hasher.ComputeHash($certificate.RawData) }
    finally { $hasher.Dispose() }
    $shaHex = [System.BitConverter]::ToString($sha).Replace("-", "").ToLowerInvariant()
    $record = [ordered]@{
        schemaVersion = 1
        purpose = "hase-minipc-python-client-authority"
        thumbprint = $certificate.Thumbprint
        certificateSha256 = $shaHex
        personalStore = "CurrentUser/My"
        trustedStore = "CurrentUser/Root"
        createdAtUtc = $now.ToString("O")
    }
    $utf8 = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText(
        $rollback, ($record | ConvertTo-Json -Depth 4), $utf8)
    $rollbackWritten = $true
    [System.IO.File]::WriteAllText(
        $manifest, ($record | ConvertTo-Json -Depth 4), $utf8)
    $manifestWritten = $true

    Write-Host "Dedicated authority created : True"
    Write-Host "Private key non-exported     : True"
    Write-Host "Public root trusted locally  : True"
    Write-Host "Server certificate unchanged : True"
    Write-Host "Rollback evidence recorded   : True"
    Write-Host "MiniPC client authority ready: True"
}
catch
{
    if ($manifestWritten) { Remove-Item -LiteralPath $manifest -Force }
    if ($rollbackWritten) { Remove-Item -LiteralPath $rollback -Force }
    if ($rootAdded -and $null -ne $certificate)
    { Remove-Item -LiteralPath ("Cert:\CurrentUser\Root\" + $certificate.Thumbprint) -Force }
    if ($null -ne $certificate)
    { Remove-Item -LiteralPath ("Cert:\CurrentUser\My\" + $certificate.Thumbprint) -Force }
    Write-Error "MiniPC Python client authority creation failed."
    exit 1
}
finally
{
    if ($null -ne $certificate) { $certificate.Dispose() }
}
