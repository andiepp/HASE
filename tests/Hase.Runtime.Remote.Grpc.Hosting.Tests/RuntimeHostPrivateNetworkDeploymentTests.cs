using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Reflection;
using Northbound = global::Hase.Runtime.Northbound;

namespace Hase.Runtime.Remote.Grpc.Hosting.Tests;

public sealed class RuntimeHostPrivateNetworkDeploymentTests
{
    [Fact]
    public void CreateAsync_DiagnosticProjection_ShouldRemainOptionalAndLast()
    {
        MethodInfo method = Assert.Single(
            typeof(RuntimeHostPrivateNetworkDeployment)
                .GetMethods(BindingFlags.Public | BindingFlags.Static),
            candidate => candidate.Name == nameof(
                RuntimeHostPrivateNetworkDeployment.CreateAsync));
        ParameterInfo parameter = method.GetParameters().Last();

        Assert.Equal("diagnosticProjectionService", parameter.Name);
        Assert.Equal(
            typeof(Northbound.RuntimeHostDiagnosticProjectionService),
            parameter.ParameterType);
        Assert.True(parameter.HasDefaultValue);
        Assert.Null(parameter.DefaultValue);
    }

    [Fact]
    public async Task CreateAsync_MissingOptions_ShouldThrow()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            "options",
            () =>
                RuntimeHostPrivateNetworkDeployment.CreateAsync(
                    null!,
                    new TestSnapshotProvider()));
    }

    [Fact]
    public async Task CreateAsync_MissingSnapshotProvider_ShouldThrow()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            "snapshotProvider",
            () =>
                RuntimeHostPrivateNetworkDeployment.CreateAsync(
                    CreateOptions(),
                    null!));
    }

    [Fact]
    public async Task CreateAsync_PreCancelled_ShouldThrowBeforeStoreAccess()
    {
        using var cancellationSource =
            new CancellationTokenSource();

        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () =>
                RuntimeHostPrivateNetworkDeployment.CreateAsync(
                    CreateOptions(),
                    new TestSnapshotProvider(),
                    cancellationToken:
                        cancellationSource.Token));
    }

    private static RuntimeHostPrivateNetworkDeploymentOptions CreateOptions()
    {
        return new RuntimeHostPrivateNetworkDeploymentOptions(
            new PrivateNetworkGrpcBinding(
                IPAddress.Parse(
                    "192.0.2.10"),
                5000),
            new RuntimeHostCertificateStoreReference(
                StoreName.My,
                StoreLocation.CurrentUser,
                "0123456789ABCDEF0123456789ABCDEF01234567"),
            Path.Combine(
                Path.GetTempPath(),
                "hase-private-network-deployment",
                "client-enrollments.json"));
    }

    private sealed class TestSnapshotProvider
        : Northbound.IRuntimeHostSnapshotProvider
    {
        public Northbound.PublishedRuntimeHostSnapshot Capture()
        {
            return new Northbound.PublishedRuntimeHostSnapshot(
                new Northbound.RuntimeHostId(
                    "runtime-host-private-network-deployment-test"),
                Northbound.RuntimeHostApiVersion.Current,
                Array.Empty<
                    Northbound.PublishedRuntimeEndpointSnapshot>());
        }
    }
}
