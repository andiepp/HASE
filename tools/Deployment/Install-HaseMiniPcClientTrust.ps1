[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PublicServerCertificatePath,

    [Parameter(Mandatory = $true)]
    [System.Net.IPAddress]$ListenerAddress,

    [Parameter(Mandatory = $true)]
    [ValidateRange(1, 65535)]
    [int]$Port,

    [Parameter(Mandatory = $true)]
    [string]$ExistingClientConfigurationPath,

    [Parameter(Mandatory = $true)]
    [string]$ClientRegistryPath,

    [Parameter(Mandatory = $true)]
    [string]$OutputConfigurationPath
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if ([System.Environment]::OSVersion.Platform -ne [System.PlatformID]::Win32NT) {
    throw "This MiniPC Client trust workflow requires Windows."
}

if ($ListenerAddress.AddressFamily -ne [System.Net.Sockets.AddressFamily]::InterNetwork -or
    [System.Net.IPAddress]::IsLoopback($ListenerAddress) -or
    $ListenerAddress.Equals([System.Net.IPAddress]::Any)) {
    throw "The MiniPC listener must be an explicit non-loopback IPv4 address."
}

$certificateSourcePath = [System.IO.Path]::GetFullPath($PublicServerCertificatePath)
$existingConfigurationPath = [System.IO.Path]::GetFullPath($ExistingClientConfigurationPath)
$registryPath = [System.IO.Path]::GetFullPath($ClientRegistryPath)
$outputPath = [System.IO.Path]::GetFullPath($OutputConfigurationPath)

foreach ($requiredFile in @(
    $certificateSourcePath,
    $existingConfigurationPath,
    $registryPath
)) {
    if (-not [System.IO.File]::Exists($requiredFile)) {
        throw "A required MiniPC Client trust source file does not exist."
    }
}

if ([System.IO.File]::Exists($outputPath)) {
    throw "MiniPC Client trust installation refused because the target configuration already exists."
}

$existingConfigurationHash = (
    Get-FileHash -LiteralPath $existingConfigurationPath -Algorithm SHA256
).Hash
$registryHash = (
    Get-FileHash -LiteralPath $registryPath -Algorithm SHA256
).Hash

$existingConfiguration = Get-Content `
    -LiteralPath $existingConfigurationPath `
    -Raw |
    ConvertFrom-Json

if ($null -eq $existingConfiguration.clientCertificate) {
    throw "The existing Client configuration has no client-certificate reference."
}

$sourceCertificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
    $certificateSourcePath)
$trustedCertificatePath = "Cert:\CurrentUser\TrustedPeople\$($sourceCertificate.Thumbprint)"
$certificateAlreadyPresent = Test-Path -LiteralPath $trustedCertificatePath
$certificateImported = $false
$published = $false

$outputDirectory = Split-Path -Parent $outputPath
[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
$temporaryPath = Join-Path `
    $outputDirectory `
    ("." + [System.IO.Path]::GetFileName($outputPath) + "." + [System.Guid]::NewGuid().ToString("N") + ".tmp")

try {
    if (-not $certificateAlreadyPresent) {
        $importedCertificate = Import-Certificate `
            -FilePath $certificateSourcePath `
            -CertStoreLocation "Cert:\CurrentUser\TrustedPeople"
        $certificateImported = $true
        if (-not [string]::Equals(
            $importedCertificate.Thumbprint,
            $sourceCertificate.Thumbprint,
            [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "The imported MiniPC server certificate does not match its public source."
        }
    }

    $document = [ordered]@{
        formatVersion = 1
        address = "https://$($ListenerAddress.IPAddressToString):$Port"
        clientCertificate = [ordered]@{
            storeName = [string]$existingConfiguration.clientCertificate.storeName
            storeLocation = [string]$existingConfiguration.clientCertificate.storeLocation
            thumbprint = [string]$existingConfiguration.clientCertificate.thumbprint
        }
        trustedServerCertificate = [ordered]@{
            storeName = "TrustedPeople"
            storeLocation = "CurrentUser"
            thumbprint = $sourceCertificate.Thumbprint
        }
    }

    $utf8WithoutBom = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText(
        $temporaryPath,
        ($document | ConvertTo-Json -Depth 5),
        $utf8WithoutBom)

    $repositoryRoot = [System.IO.Path]::GetFullPath(
        (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))
    $projectPath = Join-Path $repositoryRoot `
        "src\Hase.DesktopHost.Preflight\Hase.DesktopHost.Preflight.csproj"

    & dotnet run --project $projectPath -c Release --no-build -- `
        validate-client-provisioning `
        $temporaryPath `
        $certificateSourcePath
    if ($LASTEXITCODE -ne 0) {
        throw "The MiniPC Client security configuration failed strict validation."
    }

    $existingConfigurationUnchanged = [string]::Equals(
        $existingConfigurationHash,
        (Get-FileHash -LiteralPath $existingConfigurationPath -Algorithm SHA256).Hash,
        [System.StringComparison]::Ordinal)
    $registryUnchanged = [string]::Equals(
        $registryHash,
        (Get-FileHash -LiteralPath $registryPath -Algorithm SHA256).Hash,
        [System.StringComparison]::Ordinal)

    if (-not $existingConfigurationUnchanged -or -not $registryUnchanged) {
        throw "Existing Client state changed during MiniPC trust installation."
    }

    [System.IO.File]::Move($temporaryPath, $outputPath)
    $published = $true

    Write-Host
    Write-Host "HASE MiniPC Client trust installation succeeded."
    Write-Host "MiniPC client configuration : Ready"
    Write-Host "Existing client private key : Preserved"
    Write-Host "Public server certificate   : TrustedPeople"
    Write-Host "Existing Client registry    : Preserved"
    Write-Host "Sensitive deployment values : Withheld"
}
finally {
    $sourceCertificate.Dispose()
    Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
    if (-not $published -and $certificateImported) {
        Remove-Item -LiteralPath $trustedCertificatePath -Force -ErrorAction SilentlyContinue
    }
}
