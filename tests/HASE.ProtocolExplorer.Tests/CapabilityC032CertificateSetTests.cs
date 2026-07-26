using System.Net;
using System.Security.Cryptography.X509Certificates;
using Hase.ProtocolExplorer.Scenarios;
using Xunit;

namespace Hase.ProtocolExplorer.Tests;

public sealed class CapabilityC032CertificateSetTests
{
    private static readonly DateTimeOffset ValidationTimeUtc =
        new(
            2026,
            7,
            26,
            12,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public void Create_ShouldReturnPrivateKeyCertificates()
    {
        using CapabilityC032CertificateSet certificates =
            CapabilityC032CertificateSet.Create(
                ValidationTimeUtc);

        Assert.True(
            certificates.CertificateAuthority.HasPrivateKey);
        Assert.True(
            certificates.ServerCertificate.HasPrivateKey);
        Assert.True(
            certificates.ClientCertificate.HasPrivateKey);
    }

    [Fact]
    public void Create_ShouldIssueServerAndClientFromCertificateAuthority()
    {
        using CapabilityC032CertificateSet certificates =
            CapabilityC032CertificateSet.Create(
                ValidationTimeUtc);

        Assert.Equal(
            certificates.CertificateAuthority.Subject,
            certificates.CertificateAuthority.Issuer);
        Assert.Equal(
            certificates.CertificateAuthority.Subject,
            certificates.ServerCertificate.Issuer);
        Assert.Equal(
            certificates.CertificateAuthority.Subject,
            certificates.ClientCertificate.Issuer);
    }

    [Fact]
    public void Create_ServerCertificate_ShouldHaveServerAuthenticationUsage()
    {
        using CapabilityC032CertificateSet certificates =
            CapabilityC032CertificateSet.Create(
                ValidationTimeUtc);

        X509EnhancedKeyUsageExtension extension =
            Assert.Single(
                certificates.ServerCertificate.Extensions
                    .OfType<X509EnhancedKeyUsageExtension>());

        Assert.Equal(
            "1.3.6.1.5.5.7.3.1",
            Assert.Single(
                extension.EnhancedKeyUsages.Cast<
                    System.Security.Cryptography.Oid>()).Value);
    }

    [Fact]
    public void Create_ClientCertificate_ShouldHaveClientAuthenticationUsage()
    {
        using CapabilityC032CertificateSet certificates =
            CapabilityC032CertificateSet.Create(
                ValidationTimeUtc);

        X509EnhancedKeyUsageExtension extension =
            Assert.Single(
                certificates.ClientCertificate.Extensions
                    .OfType<X509EnhancedKeyUsageExtension>());

        Assert.Equal(
            "1.3.6.1.5.5.7.3.2",
            Assert.Single(
                extension.EnhancedKeyUsages.Cast<
                    System.Security.Cryptography.Oid>()).Value);
    }

    [Fact]
    public void Create_ServerCertificate_ShouldCoverLocalhost()
    {
        using CapabilityC032CertificateSet certificates =
            CapabilityC032CertificateSet.Create(
                ValidationTimeUtc);

        X509SubjectAlternativeNameExtension extension =
            Assert.Single(
                certificates.ServerCertificate.Extensions
                    .OfType<X509SubjectAlternativeNameExtension>());

        Assert.Contains(
            "localhost",
            extension.EnumerateDnsNames());
        Assert.Contains(
            IPAddress.Loopback,
            extension.EnumerateIPAddresses());
    }

    [Fact]
    public void Dispose_CalledTwice_ShouldBeIdempotent()
    {
        CapabilityC032CertificateSet certificates =
            CapabilityC032CertificateSet.Create(
                ValidationTimeUtc);

        certificates.Dispose();

        certificates.Dispose();
    }
}
