using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Hase.Runtime.Remote.Grpc.Hosting.Tests;

public sealed class
    RuntimeHostPrivateNetworkServerCertificateValidatorTests
{
    private static readonly IPAddress ListenerAddress =
        IPAddress.Parse(
            "192.0.2.10");

    private static readonly DateTimeOffset ValidationTimeUtc =
        new(
            2026,
            7,
            26,
            12,
            0,
            0,
            TimeSpan.Zero);

    private static readonly PrivateNetworkGrpcBinding Binding =
        new(
            ListenerAddress,
            5000);

    [Fact]
    public void Validate_MissingCertificate_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "certificate",
            () =>
                RuntimeHostPrivateNetworkServerCertificateValidator.Validate(
                    null!,
                    Binding,
                    ValidationTimeUtc));
    }

    [Fact]
    public void Validate_MissingBinding_ShouldThrow()
    {
        using X509Certificate2 certificate =
            CreateCertificate();

        Assert.Throws<ArgumentNullException>(
            "binding",
            () =>
                RuntimeHostPrivateNetworkServerCertificateValidator.Validate(
                    certificate,
                    null!,
                    ValidationTimeUtc));
    }

    [Fact]
    public void Validate_NonUtcTime_ShouldThrow()
    {
        using X509Certificate2 certificate =
            CreateCertificate();
        DateTimeOffset nonUtcTime =
            new(
                2026,
                7,
                26,
                14,
                0,
                0,
                TimeSpan.FromHours(
                    2));

        Assert.Throws<ArgumentException>(
            "validationTimeUtc",
            () =>
                RuntimeHostPrivateNetworkServerCertificateValidator.Validate(
                    certificate,
                    Binding,
                    nonUtcTime));
    }

    [Fact]
    public void Validate_MissingPrivateKey_ShouldThrow()
    {
        using X509Certificate2 certificateWithPrivateKey =
            CreateCertificate();
        using X509Certificate2 publicCertificate =
            X509CertificateLoader.LoadCertificate(
                certificateWithPrivateKey.Export(
                    X509ContentType.Cert));

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    RuntimeHostPrivateNetworkServerCertificateValidator
                        .Validate(
                            publicCertificate,
                            Binding,
                            ValidationTimeUtc));

        Assert.Contains(
            "private key",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_NotYetValidCertificate_ShouldThrow()
    {
        using X509Certificate2 certificate =
            CreateCertificate(
                notBefore:
                    ValidationTimeUtc.AddMinutes(
                        1));

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    RuntimeHostPrivateNetworkServerCertificateValidator
                        .Validate(
                            certificate,
                            Binding,
                            ValidationTimeUtc));

        Assert.Contains(
            "not yet valid",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_ExpiredCertificate_ShouldThrow()
    {
        using X509Certificate2 certificate =
            CreateCertificate(
                notAfter:
                    ValidationTimeUtc.AddMinutes(
                        -1));

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    RuntimeHostPrivateNetworkServerCertificateValidator
                        .Validate(
                            certificate,
                            Binding,
                            ValidationTimeUtc));

        Assert.Contains(
            "expired",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_ClientAuthenticationOnlyCertificate_ShouldThrow()
    {
        using X509Certificate2 certificate =
            CreateCertificate(
                enhancedKeyUsage:
                    "1.3.6.1.5.5.7.3.2");

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    RuntimeHostPrivateNetworkServerCertificateValidator
                        .Validate(
                            certificate,
                            Binding,
                            ValidationTimeUtc));

        Assert.Contains(
            "server authentication",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_DifferentListenerIdentity_ShouldThrow()
    {
        using X509Certificate2 certificate =
            CreateCertificate(
                certificateAddress:
                    IPAddress.Parse(
                        "192.0.2.11"));

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    RuntimeHostPrivateNetworkServerCertificateValidator
                        .Validate(
                            certificate,
                            Binding,
                            ValidationTimeUtc));

        Assert.Contains(
            "listener address",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_ValidCertificate_ShouldReturn()
    {
        using X509Certificate2 certificate =
            CreateCertificate();

        RuntimeHostPrivateNetworkServerCertificateValidator.Validate(
            certificate,
            Binding,
            ValidationTimeUtc);
    }

    [Fact]
    public void Validate_ValidCertificateWithoutEnhancedKeyUsage_ShouldReturn()
    {
        using X509Certificate2 certificate =
            CreateCertificate(
                enhancedKeyUsage:
                    null);

        RuntimeHostPrivateNetworkServerCertificateValidator.Validate(
            certificate,
            Binding,
            ValidationTimeUtc);
    }

    private static X509Certificate2 CreateCertificate(
        DateTimeOffset? notBefore = null,
        DateTimeOffset? notAfter = null,
        string? enhancedKeyUsage = "1.3.6.1.5.5.7.3.1",
        IPAddress? certificateAddress = null)
    {
        using RSA rsa =
            RSA.Create(
                2048);
        CertificateRequest request =
            new(
                "CN=HASE generated server-certificate test",
                rsa,
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
                X509KeyUsageFlags.DigitalSignature
                | X509KeyUsageFlags.KeyEncipherment,
                true));

        if (enhancedKeyUsage is not null)
        {
            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    new OidCollection
                    {
                        new(
                            enhancedKeyUsage)
                    },
                    true));
        }

        var subjectAlternativeName =
            new SubjectAlternativeNameBuilder();
        subjectAlternativeName.AddIpAddress(
            certificateAddress
            ?? ListenerAddress);
        request.CertificateExtensions.Add(
            subjectAlternativeName.Build());

        return request.CreateSelfSigned(
            notBefore
            ?? ValidationTimeUtc.AddDays(
                -1),
            notAfter
            ?? ValidationTimeUtc.AddDays(
                1));
    }
}
