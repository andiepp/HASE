using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Hase.Runtime.Remote.Grpc.Hosting.Tests;

public sealed class RuntimeHostPrivateNetworkGrpcClientTests
{
    private static readonly Uri Address =
        new(
            "https://192.0.2.10:5000");

    [Fact]
    public void Create_MissingAddress_ShouldThrow()
    {
        using X509Certificate2 clientCertificate =
            CreateCertificate();
        using X509Certificate2 serverCertificate =
            CreateCertificate();

        Assert.Throws<ArgumentNullException>(
            "address",
            () =>
                RuntimeHostPrivateNetworkGrpcClient.Create(
                    null!,
                    clientCertificate,
                    serverCertificate));
    }

    [Fact]
    public void Create_CleartextAddress_ShouldThrow()
    {
        using X509Certificate2 clientCertificate =
            CreateCertificate();
        using X509Certificate2 serverCertificate =
            CreateCertificate();

        Assert.Throws<ArgumentException>(
            "address",
            () =>
                RuntimeHostPrivateNetworkGrpcClient.Create(
                    new Uri(
                        "http://192.0.2.10:5000"),
                    clientCertificate,
                    serverCertificate));
    }

    [Fact]
    public void Create_MissingClientCertificate_ShouldThrow()
    {
        using X509Certificate2 serverCertificate =
            CreateCertificate();

        Assert.Throws<ArgumentNullException>(
            "clientCertificate",
            () =>
                RuntimeHostPrivateNetworkGrpcClient.Create(
                    Address,
                    null!,
                    serverCertificate));
    }

    [Fact]
    public void Create_MissingTrustedServerCertificate_ShouldThrow()
    {
        using X509Certificate2 clientCertificate =
            CreateCertificate();

        Assert.Throws<ArgumentNullException>(
            "trustedServerCertificate",
            () =>
                RuntimeHostPrivateNetworkGrpcClient.Create(
                    Address,
                    clientCertificate,
                    null!));
    }

    [Fact]
    public void Create_ClientCertificateWithoutPrivateKey_ShouldThrow()
    {
        using X509Certificate2 certificateWithPrivateKey =
            CreateCertificate();
        using X509Certificate2 clientCertificate =
            X509CertificateLoader.LoadCertificate(
                certificateWithPrivateKey.Export(
                    X509ContentType.Cert));
        using X509Certificate2 serverCertificate =
            CreateCertificate();

        Assert.Throws<ArgumentException>(
            "clientCertificate",
            () =>
                RuntimeHostPrivateNetworkGrpcClient.Create(
                    Address,
                    clientCertificate,
                    serverCertificate));
    }

    [Fact]
    public void Create_ValidConfiguration_ShouldExposeClientUntilDisposed()
    {
        using X509Certificate2 clientCertificate =
            CreateCertificate();
        using X509Certificate2 serverCertificate =
            CreateCertificate();
        RuntimeHostPrivateNetworkGrpcClient client =
            RuntimeHostPrivateNetworkGrpcClient.Create(
                Address,
                clientCertificate,
                serverCertificate);

        Assert.NotNull(
            client.Client);

        client.Dispose();
        client.Dispose();

        Assert.Throws<ObjectDisposedException>(
            () =>
                client.Client);
    }

    private static X509Certificate2 CreateCertificate()
    {
        using RSA rsa =
            RSA.Create(
                2048);
        CertificateRequest request =
            new(
                "CN=HASE generated private-network client test",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(
                -1),
            DateTimeOffset.UtcNow.AddMinutes(
                5));
    }
}
