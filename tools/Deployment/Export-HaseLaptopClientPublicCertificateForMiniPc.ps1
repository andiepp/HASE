[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$DestinationPath,

    [string]$MiniPcPrivateNetworkConfigurationPath = (
        [System.IO.Path]::Combine(
            $env:LOCALAPPDATA,
            "HASE",
            "Client",
            "Configuration",
            "minipc-private-network.json")
    )
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Get-CertificateCredentialId {
    param(
        [Parameter(Mandatory = $true)]
        [System.Security.Cryptography.X509Certificates.X509Certificate2]
        $Certificate
    )

    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hash = $sha256.ComputeHash($Certificate.RawData)
    }
    finally {
        $sha256.Dispose()
    }

    $hexValue = [System.BitConverter]::ToString($hash)
    $hexValue = $hexValue.Replace("-", "").ToLowerInvariant()
    return "x509-sha256:$hexValue"
}

if ([System.Environment]::OSVersion.Platform -ne [System.PlatformID]::Win32NT) {
    throw "The laptop Client public-certificate export requires Windows."
}

if (-not [System.IO.Path]::IsPathRooted($DestinationPath) -or
    $DestinationPath -match '^[A-Za-z]:[^\\/]') {
    throw "The public-certificate destination path must be fully qualified."
}

$destination = [System.IO.Path]::GetFullPath($DestinationPath)
$configurationPath = [System.IO.Path]::GetFullPath(
    $MiniPcPrivateNetworkConfigurationPath)

if ([System.IO.Path]::GetExtension($destination) -ine ".cer") {
    throw "The public-certificate destination must use the .cer extension."
}
if (Test-Path -LiteralPath $destination) {
    throw "Public-certificate export refused because the destination already exists."
}
if (-not (Test-Path -LiteralPath $configurationPath -PathType Leaf)) {
    throw "The MiniPC Client private-network configuration was not found."
}

$configurationHash = (
    Get-FileHash -LiteralPath $configurationPath -Algorithm SHA256
).Hash
$configuration = Get-Content -LiteralPath $configurationPath -Raw |
    ConvertFrom-Json

if ([string]$configuration.clientCertificate.storeName -cne "My" -or
    [string]$configuration.clientCertificate.storeLocation -cne "CurrentUser") {
    throw "The MiniPC Client certificate must use CurrentUser\\My custody."
}

$configuredThumbprint = [string]$configuration.clientCertificate.thumbprint
$certificates = @(
    Get-ChildItem "Cert:\CurrentUser\My" |
        Where-Object { $_.Thumbprint -eq $configuredThumbprint }
)
if ($certificates.Count -ne 1 -or -not $certificates[0].HasPrivateKey) {
    throw "The configured laptop Client certificate is not uniquely ready with its private key."
}

$credentialId = Get-CertificateCredentialId -Certificate $certificates[0]
if ($credentialId -cnotmatch '^x509-sha256:[0-9a-f]{64}$') {
    throw "The laptop Client certificate identity is invalid."
}

$destinationDirectory = Split-Path -Parent $destination
[System.IO.Directory]::CreateDirectory($destinationDirectory) | Out-Null
$temporaryPath = Join-Path `
    $destinationDirectory `
    ("." + [System.IO.Path]::GetFileName($destination) + "." +
        [System.Guid]::NewGuid().ToString("N") + ".tmp")
$published = $false
$exportedCertificate = $null

try {
    Export-Certificate `
        -Cert $certificates[0] `
        -FilePath $temporaryPath `
        -Type CERT | Out-Null

    $exportedCertificate =
        [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
            $temporaryPath)
    if ($exportedCertificate.HasPrivateKey) {
        throw "The transfer certificate unexpectedly contains a private key."
    }
    if ((Get-CertificateCredentialId -Certificate $exportedCertificate) -cne
        $credentialId) {
        throw "The exported public certificate does not match the configured Client certificate."
    }
    if ((Get-FileHash -LiteralPath $configurationPath -Algorithm SHA256).Hash -cne
        $configurationHash) {
        throw "The MiniPC Client configuration changed during public-certificate export."
    }

    [System.IO.File]::Move($temporaryPath, $destination)
    $published = $true

    Write-Host
    Write-Host "HASE laptop Client public-certificate export succeeded."
    Write-Host "MiniPC Client configuration : Preserved"
    Write-Host "Existing client private key : Preserved"
    Write-Host "Exported certificate        : Public only"
    Write-Host "Credential identity match   : Ready"
    Write-Host "Sensitive deployment values : Withheld"
}
finally {
    if ($null -ne $exportedCertificate) {
        $exportedCertificate.Dispose()
    }
    if (-not $published) {
        Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
    }
}
