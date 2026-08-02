[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PublicClientCertificatePath,

    [string]$RuntimeHostPrivateNetworkConfigurationPath = (
        [System.IO.Path]::Combine(
            $env:LOCALAPPDATA,
            "HASE",
            "RuntimeHost",
            "Configuration",
            "desktop-private-network.json")
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
    throw "The MiniPC inbound Client trust installation requires Windows."
}
if (@(Get-Process -Name "Hase.DesktopHost.App" -ErrorAction SilentlyContinue).Count -ne 0) {
    throw "The MiniPC Runtime Host must be stopped before installing inbound Client trust."
}

$certificatePath = [System.IO.Path]::GetFullPath($PublicClientCertificatePath)
$configurationPath = [System.IO.Path]::GetFullPath(
    $RuntimeHostPrivateNetworkConfigurationPath)

if ([System.IO.Path]::GetExtension($certificatePath) -ine ".cer") {
    throw "The inbound Client trust source must use the .cer extension."
}
foreach ($requiredFile in @($certificatePath, $configurationPath)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "A required MiniPC inbound Client trust file does not exist."
    }
}

$configuration = Get-Content -LiteralPath $configurationPath -Raw |
    ConvertFrom-Json
$enrollmentPath = [System.IO.Path]::GetFullPath(
    [string]$configuration.clientEnrollmentFilePath)
if (-not (Test-Path -LiteralPath $enrollmentPath -PathType Leaf)) {
    throw "The installed MiniPC Client enrollment was not found."
}

$configurationHash = (
    Get-FileHash -LiteralPath $configurationPath -Algorithm SHA256
).Hash
$enrollmentHash = (
    Get-FileHash -LiteralPath $enrollmentPath -Algorithm SHA256
).Hash
$enrollment = Get-Content -LiteralPath $enrollmentPath -Raw |
    ConvertFrom-Json
$sourceCertificate =
    [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
        $certificatePath)
$certificateImported = $false
$trustedCertificatePath = $null

try {
    if ($sourceCertificate.HasPrivateKey) {
        throw "The transferred laptop Client certificate must not contain a private key."
    }

    $credentialId = Get-CertificateCredentialId -Certificate $sourceCertificate
    $matchingEnrollments = @(
        $enrollment.enrollments |
            Where-Object { [string]$_.credentialId -ceq $credentialId }
    )
    if ($enrollment.formatVersion -ne 1 -or $matchingEnrollments.Count -ne 1) {
        throw "The laptop Client public certificate does not match exactly one MiniPC enrollment."
    }

    $trustedCertificatePath =
        "Cert:\CurrentUser\TrustedPeople\$($sourceCertificate.Thumbprint)"
    $existingCertificates = @(
        Get-ChildItem "Cert:\CurrentUser\TrustedPeople" |
            Where-Object { $_.Thumbprint -eq $sourceCertificate.Thumbprint }
    )

    if ($existingCertificates.Count -gt 1) {
        throw "The MiniPC trusted Client certificate state is ambiguous."
    }
    if ($existingCertificates.Count -eq 1) {
        $existingRawData = [System.Convert]::ToBase64String(
            $existingCertificates[0].RawData)
        $sourceRawData = [System.Convert]::ToBase64String(
            $sourceCertificate.RawData)
        if ($existingCertificates[0].HasPrivateKey -or
            $existingRawData -cne $sourceRawData) {
            throw "The existing MiniPC trusted Client certificate conflicts with the transfer source."
        }
    }
    else {
        $imported = Import-Certificate `
            -FilePath $certificatePath `
            -CertStoreLocation "Cert:\CurrentUser\TrustedPeople"
        $certificateImported = $true
        if ($imported.Thumbprint -ne $sourceCertificate.Thumbprint) {
            throw "The imported laptop Client certificate does not match the transfer source."
        }
    }

    $trustedCertificates = @(
        Get-ChildItem "Cert:\CurrentUser\TrustedPeople" |
            Where-Object { $_.Thumbprint -eq $sourceCertificate.Thumbprint }
    )
    $trustedRawData = if ($trustedCertificates.Count -eq 1) {
        [System.Convert]::ToBase64String($trustedCertificates[0].RawData)
    }
    else {
        $null
    }
    $sourceRawData = [System.Convert]::ToBase64String(
        $sourceCertificate.RawData)
    if ($trustedCertificates.Count -ne 1 -or
        $trustedCertificates[0].HasPrivateKey -or
        $trustedRawData -cne $sourceRawData) {
        throw "The MiniPC trusted Client certificate failed post-installation validation."
    }
    if ((Get-FileHash -LiteralPath $configurationPath -Algorithm SHA256).Hash -cne
        $configurationHash -or
        (Get-FileHash -LiteralPath $enrollmentPath -Algorithm SHA256).Hash -cne
        $enrollmentHash) {
        throw "MiniPC Runtime Host security configuration changed during Client trust installation."
    }

    Write-Host
    Write-Host "HASE MiniPC inbound Client trust installation succeeded."
    Write-Host "Client enrollment          : Matched"
    Write-Host "Trusted Client certificate : CurrentUser\\TrustedPeople"
    Write-Host "Transferred certificate    : Public only"
    Write-Host "Runtime Host configuration : Preserved"
    Write-Host "Runtime Host identity      : Preserved"
    Write-Host "Sensitive deployment values: Withheld"
}
catch {
    if ($certificateImported -and $null -ne $trustedCertificatePath) {
        Remove-Item -LiteralPath $trustedCertificatePath -Force -ErrorAction SilentlyContinue
    }
    throw
}
finally {
    $sourceCertificate.Dispose()
}
