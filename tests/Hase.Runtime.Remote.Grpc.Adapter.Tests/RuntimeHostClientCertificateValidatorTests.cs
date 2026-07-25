using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostClientCertificateValidatorTests
{
    private static readonly DateTimeOffset ValidationTimeUtc =
        new(
            2026,
            7,
            25,
            22,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public void Validate_MissingCertificate_ShouldFail()
    {
        RuntimeHostClientCertificateValidator validator =
            new();

        RuntimeHostClientCertificateValidationResult result =
            validator.Validate(
                null,
                ValidationTimeUtc);

        Assert.False(
            result.IsValid);
        Assert.Equal(
            RuntimeHostClientCertificateValidationFailureReason
                .CertificateMissing,
            result.FailureReason);
    }

    [Fact]
    public void Validate_NonUtcTime_ShouldThrow()
    {
        RuntimeHostClientCertificateValidator validator =
            new();
        DateTimeOffset nonUtcTime =
            new(
                2026,
                7,
                26,
                0,
                0,
                0,
                TimeSpan.FromHours(2));

        Assert.Throws<ArgumentException>(
            "validationTimeUtc",
            () =>
                validator.Validate(
                    null,
                    nonUtcTime));
    }

    [Fact]
    public void Validate_CurrentCertificateWithClientAuthenticationUsage_ShouldPass()
    {
        using X509Certificate2 certificate =
            CreateCertificate(
                ValidationTimeUtc.AddDays(-1),
                ValidationTimeUtc.AddDays(1),
                includeEnhancedKeyUsage: true,
                includeClientAuthenticationUsage: true);
        RuntimeHostClientCertificateValidator validator =
            new();

        RuntimeHostClientCertificateValidationResult result =
            validator.Validate(
                certificate,
                ValidationTimeUtc);

        Assert.True(
            result.IsValid);
        Assert.Equal(
            RuntimeHostClientCertificateValidationFailureReason.None,
            result.FailureReason);
    }

    [Fact]
    public void Validate_CurrentCertificateWithoutEnhancedKeyUsage_ShouldPass()
    {
        using X509Certificate2 certificate =
            CreateCertificate(
                ValidationTimeUtc.AddDays(-1),
                ValidationTimeUtc.AddDays(1),
                includeEnhancedKeyUsage: false,
                includeClientAuthenticationUsage: false);
        RuntimeHostClientCertificateValidator validator =
            new();

        RuntimeHostClientCertificateValidationResult result =
            validator.Validate(
                certificate,
                ValidationTimeUtc);

        Assert.True(
            result.IsValid);
    }

    [Fact]
    public void Validate_NotYetValidCertificate_ShouldFail()
    {
        using X509Certificate2 certificate =
            CreateCertificate(
                ValidationTimeUtc.AddMinutes(1),
                ValidationTimeUtc.AddDays(1),
                includeEnhancedKeyUsage: true,
                includeClientAuthenticationUsage: true);
        RuntimeHostClientCertificateValidator validator =
            new();

        RuntimeHostClientCertificateValidationResult result =
            validator.Validate(
                certificate,
                ValidationTimeUtc);

        Assert.False(
            result.IsValid);
        Assert.Equal(
            RuntimeHostClientCertificateValidationFailureReason
                .CertificateNotYetValid,
            result.FailureReason);
    }

    [Fact]
    public void Validate_ExpiredCertificate_ShouldFail()
    {
        using X509Certificate2 certificate =
            CreateCertificate(
                ValidationTimeUtc.AddDays(-2),
                ValidationTimeUtc.AddMinutes(-1),
                includeEnhancedKeyUsage: true,
                includeClientAuthenticationUsage: true);
        RuntimeHostClientCertificateValidator validator =
            new();

        RuntimeHostClientCertificateValidationResult result =
            validator.Validate(
                certificate,
                ValidationTimeUtc);

        Assert.False(
            result.IsValid);
        Assert.Equal(
            RuntimeHostClientCertificateValidationFailureReason
                .CertificateExpired,
            result.FailureReason);
    }

    [Fact]
    public void Validate_ValidityBoundary_ShouldBeInclusive()
    {
        using X509Certificate2 certificate =
            CreateCertificate(
                ValidationTimeUtc,
                ValidationTimeUtc.AddDays(1),
                includeEnhancedKeyUsage: true,
                includeClientAuthenticationUsage: true);
        RuntimeHostClientCertificateValidator validator =
            new();

        RuntimeHostClientCertificateValidationResult result =
            validator.Validate(
                certificate,
                ValidationTimeUtc);

        Assert.True(
            result.IsValid);
    }

    [Fact]
    public void Validate_EnhancedKeyUsageWithoutClientAuthentication_ShouldFail()
    {
        using X509Certificate2 certificate =
            CreateCertificate(
                ValidationTimeUtc.AddDays(-1),
                ValidationTimeUtc.AddDays(1),
                includeEnhancedKeyUsage: true,
                includeClientAuthenticationUsage: false);
        RuntimeHostClientCertificateValidator validator =
            new();

        RuntimeHostClientCertificateValidationResult result =
            validator.Validate(
                certificate,
                ValidationTimeUtc);

        Assert.False(
            result.IsValid);
        Assert.Equal(
            RuntimeHostClientCertificateValidationFailureReason
                .MissingClientAuthenticationUsage,
            result.FailureReason);
    }

    [Fact]
    public void Validate_MalformedEnhancedKeyUsage_ShouldFail()
    {
        using X509Certificate2 certificate =
            CreateCertificateWithMalformedEnhancedKeyUsage(
                ValidationTimeUtc.AddDays(-1),
                ValidationTimeUtc.AddDays(1));
        RuntimeHostClientCertificateValidator validator =
            new();

        RuntimeHostClientCertificateValidationResult result =
            validator.Validate(
                certificate,
                ValidationTimeUtc);

        Assert.False(
            result.IsValid);
        Assert.Equal(
            RuntimeHostClientCertificateValidationFailureReason
                .MalformedCertificate,
            result.FailureReason);
    }

    private static X509Certificate2 CreateCertificate(
        DateTimeOffset notBefore,
        DateTimeOffset notAfter,
        bool includeEnhancedKeyUsage,
        bool includeClientAuthenticationUsage)
    {
        using RSA key =
            RSA.Create(
                2048);

        CertificateRequest request =
            CreateRequest(
                key);

        if (includeEnhancedKeyUsage)
        {
            OidCollection usages =
                [];

            usages.Add(
                new Oid(
                    includeClientAuthenticationUsage
                        ? "1.3.6.1.5.5.7.3.2"
                        : "1.3.6.1.5.5.7.3.1"));

            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    usages,
                    true));
        }

        return request.CreateSelfSigned(
            notBefore,
            notAfter);
    }

    private static X509Certificate2
        CreateCertificateWithMalformedEnhancedKeyUsage(
            DateTimeOffset notBefore,
            DateTimeOffset notAfter)
    {
        using RSA key =
            RSA.Create(
                2048);

        CertificateRequest request =
            CreateRequest(
                key);

        request.CertificateExtensions.Add(
            new X509Extension(
                new Oid(
                    "2.5.29.37"),
                [
                    0x01,
                    0x01,
                    0x00
                ],
                true));

        return request.CreateSelfSigned(
            notBefore,
            notAfter);
    }

    private static CertificateRequest CreateRequest(
        RSA key)
    {
        CertificateRequest request =
            new(
                new X500DistinguishedName(
                    "CN=hase-client"),
                key,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(
                false,
                false,
                0,
                true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature,
                true));

        return request;
    }
}
