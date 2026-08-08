using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Hase.Python.CredentialProvisioning;

public static class PythonClientCredentialFactory
{
    public static readonly TimeSpan MaximumValidity =
        TimeSpan.FromDays(90);

    private static readonly TimeSpan ValidityBackdating =
        TimeSpan.FromMinutes(5);

    private const string ClientAuthenticationOid =
        "1.3.6.1.5.5.7.3.2";

    public static PythonClientCredentialMaterial Create(
        X509Certificate2 signingRoot,
        DateTimeOffset utcNow,
        TimeSpan validity)
    {
        ArgumentNullException.ThrowIfNull(signingRoot);

        if (validity <= TimeSpan.Zero || validity > MaximumValidity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(validity),
                "Validity must be positive and no greater than 90 days.");
        }

        DateTimeOffset notBefore =
            utcNow.ToUniversalTime() - ValidityBackdating;
        DateTimeOffset notAfter =
            notBefore + validity;

        ValidateSigningRoot(
            signingRoot,
            notBefore,
            notAfter);

        using RSA leafKey =
            RSA.Create(2048);

        var request =
            new CertificateRequest(
                "CN=HASE Python Automation Client",
                leafKey,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(
                certificateAuthority: false,
                hasPathLengthConstraint: false,
                pathLengthConstraint: 0,
                critical: true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature,
                critical: true));

        var enhancedKeyUsages =
            new OidCollection
            {
                new Oid(
                    ClientAuthenticationOid,
                    "Client Authentication")
            };
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                enhancedKeyUsages,
                critical: true));

        byte[] serialNumber =
            RandomNumberGenerator.GetBytes(16);
        serialNumber[0] &= 0x7F;
        serialNumber[0] |= 0x01;

        byte[]? certificatePem =
            null;
        byte[]? privateKeyPem =
            null;

        try
        {
            using X509Certificate2 certificate =
                request.Create(
                    signingRoot,
                    notBefore,
                    notAfter,
                    serialNumber);

            certificatePem =
                EncodePem(
                    "CERTIFICATE",
                    certificate.RawData);

            byte[] privateKeyPkcs8 =
                leafKey.ExportPkcs8PrivateKey();
            try
            {
                privateKeyPem =
                    EncodePem(
                        "PRIVATE KEY",
                        privateKeyPkcs8);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(privateKeyPkcs8);
            }

            byte[] certificateHash =
                SHA256.HashData(certificate.RawData);
            string credentialId;
            try
            {
                credentialId =
                    "x509-sha256:"
                    + Convert.ToHexString(certificateHash).ToLowerInvariant();
            }
            finally
            {
                CryptographicOperations.ZeroMemory(certificateHash);
            }

            var result =
                new PythonClientCredentialMaterial(
                    certificatePem,
                    privateKeyPem,
                    credentialId);
            certificatePem =
                null;
            privateKeyPem =
                null;
            return result;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(serialNumber);

            if (certificatePem is not null)
            {
                CryptographicOperations.ZeroMemory(certificatePem);
            }

            if (privateKeyPem is not null)
            {
                CryptographicOperations.ZeroMemory(privateKeyPem);
            }
        }
    }

    private static void ValidateSigningRoot(
        X509Certificate2 signingRoot,
        DateTimeOffset requiredNotBefore,
        DateTimeOffset requiredNotAfter)
    {
        if (!signingRoot.HasPrivateKey)
        {
            throw new InvalidOperationException(
                "The signing root has no accessible private key.");
        }

        if (!signingRoot.SubjectName.RawData.AsSpan().SequenceEqual(
            signingRoot.IssuerName.RawData))
        {
            throw new InvalidOperationException(
                "The signing certificate is not self-issued.");
        }

        X509Extension? basicConstraintsSource =
            signingRoot.Extensions
                .Cast<X509Extension>()
                .SingleOrDefault(
                    extension =>
                        extension.Oid?.Value == "2.5.29.19");

        if (basicConstraintsSource is null)
        {
            throw new InvalidOperationException(
                "The signing root has no basic constraints.");
        }

        var basicConstraints =
            new X509BasicConstraintsExtension(
                basicConstraintsSource,
                basicConstraintsSource.Critical);

        if (!basicConstraints.CertificateAuthority)
        {
            throw new InvalidOperationException(
                "The signing certificate is not a certificate authority.");
        }

        X509Extension? keyUsageSource =
            signingRoot.Extensions
                .Cast<X509Extension>()
                .SingleOrDefault(
                    extension =>
                        extension.Oid?.Value == "2.5.29.15");

        if (keyUsageSource is null)
        {
            throw new InvalidOperationException(
                "The signing root has no key usage.");
        }

        var keyUsage =
            new X509KeyUsageExtension(
                keyUsageSource,
                keyUsageSource.Critical);

        if (!keyUsage.KeyUsages.HasFlag(
            X509KeyUsageFlags.KeyCertSign))
        {
            throw new InvalidOperationException(
                "The signing root cannot sign certificates.");
        }

        DateTimeOffset rootNotBefore =
            signingRoot.NotBefore.ToUniversalTime();
        DateTimeOffset rootNotAfter =
            signingRoot.NotAfter.ToUniversalTime();

        if (
            rootNotBefore > requiredNotBefore
            || rootNotAfter < requiredNotAfter)
        {
            throw new InvalidOperationException(
                "The signing root does not cover the requested validity.");
        }

        using RSA? signingKey =
            signingRoot.GetRSAPrivateKey();

        if (signingKey is null)
        {
            throw new InvalidOperationException(
                "The signing root does not have an RSA private key.");
        }
    }

    private static byte[] EncodePem(
        ReadOnlySpan<char> label,
        ReadOnlySpan<byte> data)
    {
        char[] pemCharacters =
            PemEncoding.Write(
                label,
                data);

        try
        {
            return Encoding.ASCII.GetBytes(pemCharacters);
        }
        finally
        {
            Array.Clear(pemCharacters);
        }
    }
}

