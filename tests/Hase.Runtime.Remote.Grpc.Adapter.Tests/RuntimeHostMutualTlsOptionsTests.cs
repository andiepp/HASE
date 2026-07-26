using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostMutualTlsOptionsTests
{
    [Fact]
    public void Disabled_ShouldCreateExplicitlyDisabledConfiguration()
    {
        RuntimeHostMutualTlsOptions options =
            RuntimeHostMutualTlsOptions.Disabled();

        Assert.False(
            options.Enabled);
        Assert.Null(
            options.ServerCertificate);
        Assert.False(
            options.RequireClientCertificate);
    }

    [Fact]
    public void EnabledWith_ShouldPreserveServerCertificate()
    {
        using X509Certificate2 certificate =
            CreateSelfSignedServerCertificate();

        RuntimeHostMutualTlsOptions options =
            RuntimeHostMutualTlsOptions.EnabledWith(
                certificate);

        Assert.True(
            options.Enabled);
        Assert.Same(
            certificate,
            options.ServerCertificate);
        Assert.True(
            options.RequireClientCertificate);
    }

    [Fact]
    public void Constructor_EnabledWithoutServerCertificate_ShouldReject()
    {
        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                () => new RuntimeHostMutualTlsOptions(
                    true,
                    null,
                    true));

        Assert.Equal(
            "serverCertificate",
            exception.ParamName);
    }

    [Fact]
    public void Constructor_EnabledWithoutRequiredClientCertificate_ShouldReject()
    {
        using X509Certificate2 certificate =
            CreateSelfSignedServerCertificate();

        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                () => new RuntimeHostMutualTlsOptions(
                    true,
                    certificate,
                    false));

        Assert.Equal(
            "requireClientCertificate",
            exception.ParamName);
    }

    [Fact]
    public void Constructor_DisabledWithServerCertificate_ShouldReject()
    {
        using X509Certificate2 certificate =
            CreateSelfSignedServerCertificate();

        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                () => new RuntimeHostMutualTlsOptions(
                    false,
                    certificate,
                    false));

        Assert.Equal(
            "serverCertificate",
            exception.ParamName);
    }

    [Fact]
    public void Constructor_DisabledWithRequiredClientCertificate_ShouldReject()
    {
        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                () => new RuntimeHostMutualTlsOptions(
                    false,
                    null,
                    true));

        Assert.Equal(
            "requireClientCertificate",
            exception.ParamName);
    }

    [Fact]
    public void EnabledWith_MissingServerCertificate_ShouldReject()
    {
        Assert.Throws<ArgumentNullException>(
            () => RuntimeHostMutualTlsOptions.EnabledWith(
                null!));
    }

    private static X509Certificate2 CreateSelfSignedServerCertificate()
    {
        using RSA rsa =
            RSA.Create(
                2048);
        CertificateRequest request =
            new(
                "CN=hase-runtime-host",
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
                X509KeyUsageFlags.DigitalSignature,
                true));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection
                {
                    new("1.3.6.1.5.5.7.3.1")
                },
                true));

        DateTimeOffset nowUtc =
            DateTimeOffset.UtcNow;

        return request.CreateSelfSigned(
            nowUtc.AddMinutes(
                -1),
            nowUtc.AddDays(
                1));
    }
}
