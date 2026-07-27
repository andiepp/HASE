using System.Security.Cryptography.X509Certificates;

namespace Hase.Runtime.Remote.Grpc.Hosting.Tests;

public sealed class RuntimeHostPrivateNetworkClientDeploymentTests
{
    [Fact]
    public void Create_MissingOptions_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "options",
            () =>
                RuntimeHostPrivateNetworkClientDeployment.Create(
                    null!));
    }

    [Fact]
    public void Create_MissingClientCertificate_ShouldFailClosed()
    {
        var options =
            new RuntimeHostPrivateNetworkClientOptions(
                new Uri(
                    "https://192.0.2.10:5000"),
                new RuntimeHostCertificateStoreReference(
                    StoreName.My,
                    StoreLocation.CurrentUser,
                    "0123456789ABCDEF0123456789ABCDEF01234567"),
                new RuntimeHostCertificateStoreReference(
                    StoreName.CertificateAuthority,
                    StoreLocation.CurrentUser,
                    "89ABCDEF0123456789ABCDEF0123456789ABCDEF"));

        Assert.Throws<InvalidOperationException>(
            () =>
                RuntimeHostPrivateNetworkClientDeployment.Create(
                    options));
    }
}
