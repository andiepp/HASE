using System.Security.Cryptography.X509Certificates;

namespace Hase.Runtime.Remote.Grpc.Hosting.Tests;

public sealed class RuntimeHostPrivateNetworkClientOptionsTests
{
    private static readonly RuntimeHostCertificateStoreReference
        ClientCertificate =
            new(
                StoreName.My,
                StoreLocation.CurrentUser,
                "0123456789ABCDEF0123456789ABCDEF01234567");

    private static readonly RuntimeHostCertificateStoreReference
        TrustedServerCertificate =
            new(
                StoreName.CertificateAuthority,
                StoreLocation.CurrentUser,
                "89ABCDEF0123456789ABCDEF0123456789ABCDEF");

    [Fact]
    public void Constructor_MissingAddress_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "address",
            () =>
                new RuntimeHostPrivateNetworkClientOptions(
                    null!,
                    ClientCertificate,
                    TrustedServerCertificate));
    }

    [Theory]
    [MemberData(nameof(InvalidAddresses))]
    public void Constructor_InvalidAddress_ShouldThrow(
        Uri address)
    {
        Assert.Throws<ArgumentException>(
            "address",
            () =>
                new RuntimeHostPrivateNetworkClientOptions(
                    address,
                    ClientCertificate,
                    TrustedServerCertificate));
    }

    [Fact]
    public void Constructor_MissingClientCertificate_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "clientCertificate",
            () =>
                new RuntimeHostPrivateNetworkClientOptions(
                    new Uri(
                        "https://192.0.2.10:5000"),
                    null!,
                    TrustedServerCertificate));
    }

    [Fact]
    public void Constructor_MissingTrustedServerCertificate_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "trustedServerCertificate",
            () =>
                new RuntimeHostPrivateNetworkClientOptions(
                    new Uri(
                        "https://192.0.2.10:5000"),
                    ClientCertificate,
                    null!));
    }

    [Fact]
    public void Constructor_ValidOptions_ShouldPreserveValues()
    {
        var address =
            new Uri(
                "https://192.0.2.10:5000");

        var options =
            new RuntimeHostPrivateNetworkClientOptions(
                address,
                ClientCertificate,
                TrustedServerCertificate);

        Assert.Same(
            address,
            options.Address);
        Assert.Same(
            ClientCertificate,
            options.ClientCertificate);
        Assert.Same(
            TrustedServerCertificate,
            options.TrustedServerCertificate);
    }

    public static TheoryData<Uri> InvalidAddresses
    {
        get;
    } =
        new()
        {
            new Uri(
                "/runtime-host",
                UriKind.Relative),
            new Uri(
                "http://192.0.2.10:5000"),
            new Uri(
                "https://runtime-host.example:5000"),
            new Uri(
                "https://192.0.2.10:5000/api"),
            new Uri(
                "https://user@192.0.2.10:5000"),
            new Uri(
                "https://192.0.2.10:5000/?query=true"),
            new Uri(
                "https://192.0.2.10:5000/#fragment")
        };
}
