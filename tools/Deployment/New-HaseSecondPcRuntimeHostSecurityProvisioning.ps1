[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [System.Net.IPAddress]$ListenerAddress,

    [Parameter(Mandatory = $true)]
    [ValidateRange(1, 65535)]
    [int]$Port,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [Parameter(Mandatory = $true)]
    [string]$LaptopClientCredentialId,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$ClientPrincipalId,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$TrustPolicyId
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if ([System.Environment]::OSVersion.Platform -ne [System.PlatformID]::Win32NT) {
    throw "This security-provisioning workflow requires Windows."
}

if ($LaptopClientCredentialId -cnotmatch '^x509-sha256:[0-9a-f]{64}$') {
    throw "The laptop client credential identity is not a normalized X.509 SHA-256 identity."
}

$ownedAddress = Get-NetIPAddress -ErrorAction Stop |
    Where-Object { $_.IPAddress -eq $ListenerAddress.IPAddressToString } |
    Select-Object -First 1
if ($null -eq $ownedAddress) {
    throw "The listener address is not assigned to this computer."
}

$outputPath = [System.IO.Path]::GetFullPath($OutputDirectory)
$configurationPath = Join-Path $outputPath "desktop-private-network.json"
$enrollmentPath = Join-Path $outputPath "client-enrollments.json"
$publicCertificatePath = Join-Path $outputPath "runtime-host-server.cer"
$targets = @($configurationPath, $enrollmentPath, $publicCertificatePath)

foreach ($target in $targets) {
    if ([System.IO.File]::Exists($target)) {
        throw "Security provisioning refused because a target file already exists."
    }
}

[System.IO.Directory]::CreateDirectory($outputPath) | Out-Null
$temporaryDirectory = Join-Path $outputPath (".provisioning-" + [System.Guid]::NewGuid().ToString("N"))
[System.IO.Directory]::CreateDirectory($temporaryDirectory) | Out-Null
$temporaryConfigurationPath = Join-Path $temporaryDirectory "desktop-private-network.json"
$temporaryEnrollmentPath = Join-Path $temporaryDirectory "client-enrollments.json"
$temporaryPublicCertificatePath = Join-Path $temporaryDirectory "runtime-host-server.cer"

$certificate = $null
$published = $false
try {
    $now = [System.DateTimeOffset]::UtcNow
    $certificate = New-SelfSignedCertificate `
        -Type Custom `
        -Subject "CN=HASE Second Runtime Host" `
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

    Export-Certificate `
        -Cert $certificate `
        -FilePath $temporaryPublicCertificatePath `
        -Type CERT | Out-Null

    $enrollmentDocument = [ordered]@{
        formatVersion = 1
        enrollments = @(
            [ordered]@{
                credentialId = $LaptopClientCredentialId
                principalId = $ClientPrincipalId
                trustPolicyId = $TrustPolicyId
            }
        )
    }
    $configurationDocument = [ordered]@{
        formatVersion = 1
        binding = [ordered]@{
            address = $ListenerAddress.IPAddressToString
            port = $Port
        }
        serverCertificate = [ordered]@{
            storeName = "My"
            storeLocation = "CurrentUser"
            thumbprint = $certificate.Thumbprint
        }
        clientEnrollmentFilePath = $enrollmentPath
    }

    $utf8WithoutBom = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText(
        $temporaryEnrollmentPath,
        ($enrollmentDocument | ConvertTo-Json -Depth 5),
        $utf8WithoutBom)
    [System.IO.File]::WriteAllText(
        $temporaryConfigurationPath,
        ($configurationDocument | ConvertTo-Json -Depth 5),
        $utf8WithoutBom)

    $repositoryRoot = [System.IO.Path]::GetFullPath(
        (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))
    $projectPath = Join-Path $repositoryRoot `
        "src\Hase.DesktopHost.Preflight\Hase.DesktopHost.Preflight.csproj"

    # Validate temporary content with final absolute references. The
    # enrollment is briefly staged at its final name so the strict reader can
    # resolve the exact path recorded in the deployment document.
    [System.IO.File]::Move($temporaryEnrollmentPath, $enrollmentPath)
    try {
        & dotnet run --project $projectPath -c Release --no-build -- `
            validate-provisioning `
            $temporaryConfigurationPath `
            $temporaryPublicCertificatePath
        if ($LASTEXITCODE -ne 0) {
            throw "The provisioned security documents failed strict validation."
        }

        [System.IO.File]::Move($temporaryConfigurationPath, $configurationPath)
        [System.IO.File]::Move($temporaryPublicCertificatePath, $publicCertificatePath)
        $published = $true
    }
    catch {
        Remove-Item -LiteralPath $enrollmentPath -Force -ErrorAction SilentlyContinue
        throw
    }

    Write-Host
    Write-Host "HASE second Runtime Host security provisioning succeeded."
    Write-Host "Private-network configuration : Ready"
    Write-Host "Client enrollment             : Ready"
    Write-Host "Server certificate private key: CurrentUser certificate store"
    Write-Host "Public server certificate     : Ready for laptop transfer"
    Write-Host "Sensitive deployment values   : Withheld"
}
finally {
    Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force -ErrorAction SilentlyContinue
    if (-not $published) {
        foreach ($target in $targets) {
            Remove-Item -LiteralPath $target -Force -ErrorAction SilentlyContinue
        }
        if ($null -ne $certificate) {
            Remove-Item `
                -LiteralPath "Cert:\CurrentUser\My\$($certificate.Thumbprint)" `
                -Force `
                -ErrorAction SilentlyContinue
        }
    }
}
