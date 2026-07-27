[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $BundleDirectory
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if ([System.Environment]::OSVersion.Platform -ne
    [System.PlatformID]::Win32NT) {
    throw "This installation workflow requires Windows."
}

$bundlePath =
    [System.IO.Path]::GetFullPath($BundleDirectory)
$clientCredentialPath =
    [System.IO.Path]::Combine(
        $bundlePath,
        "laptop-client.pfx")
$serverCertificatePath =
    [System.IO.Path]::Combine(
        $bundlePath,
        "runtime-host-server.cer")
$clientConfigurationPath =
    [System.IO.Path]::Combine(
        $bundlePath,
        "laptop-private-network.json")

foreach ($requiredFile in @(
    $clientCredentialPath,
    $serverCertificatePath,
    $clientConfigurationPath
)) {
    if (-not [System.IO.File]::Exists($requiredFile)) {
        throw "The laptop validation bundle is incomplete."
    }
}

$configuration =
    Get-Content `
        -LiteralPath $clientConfigurationPath `
        -Raw |
        ConvertFrom-Json

$importPassword =
    Read-Host `
        "Enter the transfer password for the laptop client credential" `
        -AsSecureString

$clientCertificate =
    Import-PfxCertificate `
        -FilePath $clientCredentialPath `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -Password $importPassword `
        -Exportable:$false

$serverCertificate =
    Import-Certificate `
        -FilePath $serverCertificatePath `
        -CertStoreLocation "Cert:\CurrentUser\TrustedPeople"

try {
    if (-not $clientCertificate.HasPrivateKey) {
        throw "The imported client certificate has no accessible private key."
    }

    if (-not [string]::Equals(
        $clientCertificate.Thumbprint,
        $configuration.clientCertificate.thumbprint,
        [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "The imported client certificate does not match the external configuration."
    }

    if (-not [string]::Equals(
        $serverCertificate.Thumbprint,
        $configuration.trustedServerCertificate.thumbprint,
        [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "The imported server certificate does not match the external configuration."
    }

    Write-Host
    Write-Host "HASE private-network validation client installed."
    Write-Host "Deployment values and credential identifiers are withheld."
    Write-Host "Remove the transferred client credential after validation of the installation."
}
catch {
    Remove-Item `
        -LiteralPath "Cert:\CurrentUser\My\$($clientCertificate.Thumbprint)" `
        -Force `
        -ErrorAction SilentlyContinue
    Remove-Item `
        -LiteralPath "Cert:\CurrentUser\TrustedPeople\$($serverCertificate.Thumbprint)" `
        -Force `
        -ErrorAction SilentlyContinue
    throw
}
