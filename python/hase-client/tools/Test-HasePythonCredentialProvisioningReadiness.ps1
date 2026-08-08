[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $DesktopConfigurationPath
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$failureClassification = "UnexpectedFailure"
$toolDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$packageDirectory = Split-Path -Parent $toolDirectory
$virtualEnvironmentPython =
    Join-Path $packageDirectory ".venv\Scripts\python.exe"

try
{
    $failureClassification = "PythonEnvironmentUnavailable"
    if (-not (Test-Path -LiteralPath $virtualEnvironmentPython -PathType Leaf))
    {
        throw "Unavailable"
    }

    $failureClassification = "ConfigurationInvalid"
    $configurationResult =
        & $virtualEnvironmentPython `
            -m hase._credential_readiness `
            $DesktopConfigurationPath `
            2>$null

    if ($LASTEXITCODE -ne 0)
    {
        throw "Invalid"
    }

    $configuration = $configurationResult | ConvertFrom-Json
    if (
        $null -eq $configuration `
        -or [string]::IsNullOrWhiteSpace($configuration.serverThumbprint) `
        -or [string]::IsNullOrWhiteSpace($configuration.enrollmentPath))
    {
        throw "Invalid"
    }

    $failureClassification = "ServerCredentialUnavailable"
    $personalStore =
        [System.Security.Cryptography.X509Certificates.X509Store]::new(
            [System.Security.Cryptography.X509Certificates.StoreName]::My,
            [System.Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser)
    $rootStore =
        [System.Security.Cryptography.X509Certificates.X509Store]::new(
            [System.Security.Cryptography.X509Certificates.StoreName]::Root,
            [System.Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser)

    try
    {
        $personalStore.Open(
            [System.Security.Cryptography.X509Certificates.OpenFlags]::ReadOnly)
        $rootStore.Open(
            [System.Security.Cryptography.X509Certificates.OpenFlags]::ReadOnly)

        $serverCertificates =
            @(
                $personalStore.Certificates |
                    Where-Object {
                        [string]::Equals(
                            $_.Thumbprint,
                            $configuration.serverThumbprint,
                            [System.StringComparison]::OrdinalIgnoreCase)
                    }
            )

        if ($serverCertificates.Count -ne 1)
        {
            throw "Unavailable"
        }

        $serverCertificate = $serverCertificates[0]
        $now = [System.DateTime]::UtcNow
        if (
            -not $serverCertificate.HasPrivateKey `
            -or $now -lt $serverCertificate.NotBefore.ToUniversalTime() `
            -or $now -gt $serverCertificate.NotAfter.ToUniversalTime())
        {
            throw "Unavailable"
        }

        $serverEkuExtension =
            @(
                $serverCertificate.Extensions |
                    Where-Object { $_.Oid.Value -eq "2.5.29.37" }
            )
        if ($serverEkuExtension.Count -ne 1)
        {
            throw "Unavailable"
        }
        $serverEku =
            [System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new(
                $serverEkuExtension[0],
                $serverEkuExtension[0].Critical)
        $serverEkuValues =
            @($serverEku.EnhancedKeyUsages | ForEach-Object { $_.Value })
        if (
            $serverEkuValues -notcontains "1.3.6.1.5.5.7.3.1" `
            -or $serverEkuValues -contains "1.3.6.1.5.5.7.3.2")
        {
            throw "Unavailable"
        }

        $failureClassification = "SigningRootUnavailable"
        $serverIssuer =
            [System.Convert]::ToBase64String($serverCertificate.IssuerName.RawData)
        $signingRoots =
            @(
                $personalStore.Certificates |
                    Where-Object {
                        $subject = [System.Convert]::ToBase64String($_.SubjectName.RawData)
                        $issuer = [System.Convert]::ToBase64String($_.IssuerName.RawData)
                        $subject -eq $serverIssuer `
                            -and $subject -eq $issuer `
                            -and $_.HasPrivateKey
                    }
            )

        if ($signingRoots.Count -ne 1)
        {
            throw "Unavailable"
        }

        $signingRoot = $signingRoots[0]
        if (
            $now -lt $signingRoot.NotBefore.ToUniversalTime() `
            -or $now -gt $signingRoot.NotAfter.ToUniversalTime())
        {
            throw "Unavailable"
        }

        $basicConstraints =
            @(
                $signingRoot.Extensions |
                    Where-Object { $_.Oid.Value -eq "2.5.29.19" }
            )
        if ($basicConstraints.Count -ne 1)
        {
            throw "Unavailable"
        }
        $rootConstraints =
            [System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new(
                $basicConstraints[0],
                $basicConstraints[0].Critical)
        if (-not $rootConstraints.CertificateAuthority)
        {
            throw "Unavailable"
        }

        $rootPrivateKey =
            [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey(
                $signingRoot)
        if ($null -eq $rootPrivateKey)
        {
            throw "Unavailable"
        }
        $rootPrivateKey.Dispose()

        $failureClassification = "TrustAnchorUnavailable"
        $signingRootBytes =
            [System.Convert]::ToBase64String($signingRoot.RawData)
        $trustedRoots =
            @(
                $rootStore.Certificates |
                    Where-Object {
                        [System.Convert]::ToBase64String($_.RawData) -eq $signingRootBytes
                    }
            )
        if ($trustedRoots.Count -ne 1)
        {
            throw "Unavailable"
        }
    }
    finally
    {
        $personalStore.Dispose()
        $rootStore.Dispose()
    }

    $failureClassification = "EnrollmentCustodyUnavailable"
    $enrollmentDirectory =
        Split-Path -Parent $configuration.enrollmentPath
    if (-not (Test-Path -LiteralPath $enrollmentDirectory -PathType Container))
    {
        throw "Unavailable"
    }

    Write-Host "Configuration valid       : True"
    Write-Host "Server credential ready   : True"
    Write-Host "Signing root ready        : True"
    Write-Host "Trust anchor ready        : True"
    Write-Host "Enrollment custody ready  : True"
    Write-Host "Python provisioning ready : True"
}
catch
{
    Write-Error ("Python credential readiness failed: {0}." -f $failureClassification)
    exit 1
}

