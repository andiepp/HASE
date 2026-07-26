using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostMutualTlsKestrelConfigurationFactoryTests
{
    [Fact]
    public void Create_EnabledOptions_ShouldUseHttp2Only()
    {
        using X509Certificate2 certificate =
            CreateSelfSignedServerCertificate();

        RuntimeHostMutualTlsKestrelConfiguration configuration =
            RuntimeHostMutualTlsKestrelConfigurationFactory.Create(
                RuntimeHostMutualTlsOptions.EnabledWith(
                    certificate));

        Assert.Equal(
            HttpProtocols.Http2,
            configuration.Protocols);
    }

    [Fact]
    public void Create_EnabledOptions_ShouldRequireClientCertificate()
    {
        using X509Certificate2 certificate =
            CreateSelfSignedServerCertificate();

        RuntimeHostMutualTlsKestrelConfiguration configuration =
            RuntimeHostMutualTlsKestrelConfigurationFactory.Create(
                RuntimeHostMutualTlsOptions.EnabledWith(
                    certificate));

        Assert.Equal(
            ClientCertificateMode.RequireCertificate,
            configuration.HttpsOptions.ClientCertificateMode);
    }

    [Fact]
    public void Create_EnabledOptions_ShouldAllowTls12AndTls13Only()
    {
        using X509Certificate2 certificate =
            CreateSelfSignedServerCertificate();

        RuntimeHostMutualTlsKestrelConfiguration configuration =
            RuntimeHostMutualTlsKestrelConfigurationFactory.Create(
                RuntimeHostMutualTlsOptions.EnabledWith(
                    certificate));

        Assert.Equal(
            SslProtocols.Tls12
            | SslProtocols.Tls13,
            configuration.HttpsOptions.SslProtocols);
    }

    [Fact]
    public void Create_EnabledOptions_ShouldPreserveServerCertificate()
    {
        using X509Certificate2 certificate =
            CreateSelfSignedServerCertificate();

        RuntimeHostMutualTlsKestrelConfiguration configuration =
            RuntimeHostMutualTlsKestrelConfigurationFactory.Create(
                RuntimeHostMutualTlsOptions.EnabledWith(
                    certificate));

        Assert.Same(
            certificate,
            configuration.HttpsOptions.ServerCertificate);
    }

    [Fact]
    public void Create_DisabledOptions_ShouldReject()
    {
        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    RuntimeHostMutualTlsKestrelConfigurationFactory.Create(
                        RuntimeHostMutualTlsOptions.Disabled()));

        Assert.Contains(
            "disabled",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_MissingOptions_ShouldReject()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                RuntimeHostMutualTlsKestrelConfigurationFactory.Create(
                    null!));
    }

    [Fact]
    public void Configuration_NonHttp2Protocol_ShouldReject()
    {
        HttpsConnectionAdapterOptions httpsOptions =
            new();

        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                () => new RuntimeHostMutualTlsKestrelConfiguration(
                    HttpProtocols.Http1AndHttp2,
                    httpsOptions));

        Assert.Equal(
            "protocols",
            exception.ParamName);
    }

    [Fact]
    public void Configuration_MissingHttpsOptions_ShouldReject()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostMutualTlsKestrelConfiguration(
                HttpProtocols.Http2,
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
