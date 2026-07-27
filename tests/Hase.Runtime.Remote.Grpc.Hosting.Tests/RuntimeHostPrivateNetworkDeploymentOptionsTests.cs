using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace Hase.Runtime.Remote.Grpc.Hosting.Tests;

public sealed class RuntimeHostPrivateNetworkDeploymentOptionsTests
{
    private static readonly PrivateNetworkGrpcBinding Binding =
        new(
            IPAddress.Parse(
                "192.0.2.10"),
            5000);

    private static readonly RuntimeHostCertificateStoreReference
        ServerCertificate =
            new(
                StoreName.My,
                StoreLocation.CurrentUser,
                "0123456789ABCDEF0123456789ABCDEF01234567");

    [Fact]
    public void Constructor_MissingBinding_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "binding",
            () =>
                new RuntimeHostPrivateNetworkDeploymentOptions(
                    null!,
                    ServerCertificate,
                    CreateFullyQualifiedPath()));
    }

    [Fact]
    public void Constructor_MissingServerCertificate_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "serverCertificate",
            () =>
                new RuntimeHostPrivateNetworkDeploymentOptions(
                    Binding,
                    null!,
                    CreateFullyQualifiedPath()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("client-enrollments.json")]
    public void Constructor_InvalidEnrollmentPath_ShouldThrow(
        string? clientEnrollmentFilePath)
    {
        Assert.ThrowsAny<ArgumentException>(
            () =>
                new RuntimeHostPrivateNetworkDeploymentOptions(
                    Binding,
                    ServerCertificate,
                    clientEnrollmentFilePath!));
    }

    [Fact]
    public void Constructor_ValidOptions_ShouldPreserveValues()
    {
        string clientEnrollmentFilePath =
            CreateFullyQualifiedPath();

        var options =
            new RuntimeHostPrivateNetworkDeploymentOptions(
                Binding,
                ServerCertificate,
                clientEnrollmentFilePath);

        Assert.Same(
            Binding,
            options.Binding);
        Assert.Same(
            ServerCertificate,
            options.ServerCertificate);
        Assert.Equal(
            Path.GetFullPath(
                clientEnrollmentFilePath),
            options.ClientEnrollmentFilePath);
    }

    private static string CreateFullyQualifiedPath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "hase-private-network-deployment",
            "client-enrollments.json");
    }
}
