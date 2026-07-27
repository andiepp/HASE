[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [System.Net.IPAddress] $ListenerAddress,

    [Parameter(Mandatory)]
    [ValidateRange(1, 65535)]
    [int] $Port,

    [Parameter(Mandatory)]
    [string] $OutputDirectory,

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $ClientPrincipalId = "laptop-validation-client",

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $TrustPolicyId = "private-network-validation-v1"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if ([System.Environment]::OSVersion.Platform -ne
    [System.PlatformID]::Win32NT) {
    throw "This provisioning workflow requires Windows."
}

$outputPath = [System.IO.Path]::GetFullPath($OutputDirectory)
$desktopConfigurationPath =
    [System.IO.Path]::Combine(
        $outputPath,
        "desktop-private-network.json")
$laptopConfigurationPath =
    [System.IO.Path]::Combine(
        $outputPath,
        "laptop-private-network.json")
$enrollmentPath =
    [System.IO.Path]::Combine(
        $outputPath,
        "client-enrollments.json")
$clientCredentialPath =
    [System.IO.Path]::Combine(
        $outputPath,
        "laptop-client.pfx")
$serverCertificatePath =
    [System.IO.Path]::Combine(
        $outputPath,
        "runtime-host-server.cer")

$targets = @(
    $desktopConfigurationPath,
    $laptopConfigurationPath,
    $enrollmentPath,
    $clientCredentialPath,
    $serverCertificatePath
)

foreach ($target in $targets) {
    if ([System.IO.File]::Exists($target)) {
        throw "Provisioning refused because a target file already exists."
    }
}

[System.IO.Directory]::CreateDirectory($outputPath) | Out-Null

$now = [System.DateTimeOffset]::UtcNow
$rootSubject =
    "CN=HASE Private Network Validation Root " +
    [System.Guid]::NewGuid().ToString("N")
$serverSubject =
    "CN=HASE Private Network Validation Server"
$clientSubject =
    "CN=HASE Private Network Validation Client"

$rootCertificate = $null
$serverCertificate = $null
$clientCertificate = $null
$published = $false

try {
    $rootCertificate =
        New-SelfSignedCertificate `
            -Type Custom `
            -Subject $rootSubject `
            -CertStoreLocation "Cert:\CurrentUser\My" `
            -KeyAlgorithm RSA `
            -KeyLength 3072 `
            -HashAlgorithm SHA256 `
            -KeyUsage CertSign, CRLSign `
            -TextExtension @(
                "2.5.29.19={critical}{text}ca=true&pathlength=0"
            ) `
            -NotBefore $now.AddMinutes(-5).DateTime `
            -NotAfter $now.AddYears(2).DateTime

    $serverCertificate =
        New-SelfSignedCertificate `
            -Type Custom `
            -Subject $serverSubject `
            -Signer $rootCertificate `
            -CertStoreLocation "Cert:\CurrentUser\My" `
            -KeyAlgorithm RSA `
            -KeyLength 2048 `
            -HashAlgorithm SHA256 `
            -KeyUsage DigitalSignature, KeyEncipherment `
            -TextExtension @(
                "2.5.29.19={critical}{text}ca=false",
                "2.5.29.37={critical}{text}1.3.6.1.5.5.7.3.1",
                "2.5.29.17={text}IPAddress=$($ListenerAddress.IPAddressToString)"
            ) `
            -NotBefore $now.AddMinutes(-5).DateTime `
            -NotAfter $now.AddDays(90).DateTime

    $clientCertificate =
        New-SelfSignedCertificate `
            -Type Custom `
            -Subject $clientSubject `
            -Signer $rootCertificate `
            -CertStoreLocation "Cert:\CurrentUser\My" `
            -KeyAlgorithm RSA `
            -KeyLength 2048 `
            -HashAlgorithm SHA256 `
            -KeyExportPolicy Exportable `
            -KeyUsage DigitalSignature `
            -TextExtension @(
                "2.5.29.19={critical}{text}ca=false",
                "2.5.29.37={critical}{text}1.3.6.1.5.5.7.3.2"
            ) `
            -NotBefore $now.AddMinutes(-5).DateTime `
            -NotAfter $now.AddDays(90).DateTime

    $rootStore =
        [System.Security.Cryptography.X509Certificates.X509Store]::new(
            [System.Security.Cryptography.X509Certificates.StoreName]::Root,
            [System.Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser)
    try {
        $rootStore.Open(
            [System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
        $rootStore.Add($rootCertificate)
    }
    finally {
        $rootStore.Dispose()
    }

    $exportPassword =
        Read-Host `
            "Enter a transfer password for the laptop client credential" `
            -AsSecureString

    Export-PfxCertificate `
        -Cert $clientCertificate `
        -FilePath $clientCredentialPath `
        -Password $exportPassword `
        -ChainOption EndEntityCertOnly `
        -NoProperties | Out-Null

    Export-Certificate `
        -Cert $serverCertificate `
        -FilePath $serverCertificatePath `
        -Type CERT | Out-Null

    $sha256 =
        [System.Security.Cryptography.SHA256]::Create()
    try {
        $credentialHashBytes =
            $sha256.ComputeHash(
                $clientCertificate.RawData)
    }
    finally {
        $sha256.Dispose()
    }
    $credentialHash =
        -join (
            $credentialHashBytes |
                ForEach-Object {
                    $_.ToString("x2")
                }
        )
    $credentialId =
        "x509-sha256:$credentialHash"

    $enrollmentDocument =
        [ordered]@{
            formatVersion = 1
            enrollments = @(
                [ordered]@{
                    credentialId = $credentialId
                    principalId = $ClientPrincipalId
                    trustPolicyId = $TrustPolicyId
                }
            )
        }

    $desktopDocument =
        [ordered]@{
            formatVersion = 1
            binding = [ordered]@{
                address = $ListenerAddress.IPAddressToString
                port = $Port
            }
            serverCertificate = [ordered]@{
                storeName = "My"
                storeLocation = "CurrentUser"
                thumbprint = $serverCertificate.Thumbprint
            }
            clientEnrollmentFilePath = $enrollmentPath
        }

    $laptopDocument =
        [ordered]@{
            formatVersion = 1
            address =
                "https://$($ListenerAddress.IPAddressToString):$Port"
            clientCertificate = [ordered]@{
                storeName = "My"
                storeLocation = "CurrentUser"
                thumbprint = $clientCertificate.Thumbprint
            }
            trustedServerCertificate = [ordered]@{
                storeName = "TrustedPeople"
                storeLocation = "CurrentUser"
                thumbprint = $serverCertificate.Thumbprint
            }
        }

    $utf8WithoutBom =
        [System.Text.UTF8Encoding]::new($false)

    [System.IO.File]::WriteAllText(
        $enrollmentPath,
        ($enrollmentDocument | ConvertTo-Json -Depth 5),
        $utf8WithoutBom)
    [System.IO.File]::WriteAllText(
        $desktopConfigurationPath,
        ($desktopDocument | ConvertTo-Json -Depth 5),
        $utf8WithoutBom)
    [System.IO.File]::WriteAllText(
        $laptopConfigurationPath,
        ($laptopDocument | ConvertTo-Json -Depth 5),
        $utf8WithoutBom)

    Remove-Item `
        -LiteralPath "Cert:\CurrentUser\My\$($clientCertificate.Thumbprint)" `
        -Force
    $clientCertificate = $null

    $published = $true

    Write-Host
    Write-Host "HASE private-network validation bundle created."
    Write-Host "Deployment values and credential identifiers are withheld."
    Write-Host "Transfer only the laptop credential, server certificate, and laptop configuration."
}
finally {
    if (-not $published) {
        foreach ($target in $targets) {
            Remove-Item `
                -LiteralPath $target `
                -Force `
                -ErrorAction SilentlyContinue
        }

        if ($clientCertificate) {
            Remove-Item `
                -LiteralPath "Cert:\CurrentUser\My\$($clientCertificate.Thumbprint)" `
                -Force `
                -ErrorAction SilentlyContinue
        }

        if ($serverCertificate) {
            Remove-Item `
                -LiteralPath "Cert:\CurrentUser\My\$($serverCertificate.Thumbprint)" `
                -Force `
                -ErrorAction SilentlyContinue
        }

        if ($rootCertificate) {
            Remove-Item `
                -LiteralPath "Cert:\CurrentUser\My\$($rootCertificate.Thumbprint)" `
                -Force `
                -ErrorAction SilentlyContinue
            Remove-Item `
                -LiteralPath "Cert:\CurrentUser\Root\$($rootCertificate.Thumbprint)" `
                -Force `
                -ErrorAction SilentlyContinue
        }
    }
}
