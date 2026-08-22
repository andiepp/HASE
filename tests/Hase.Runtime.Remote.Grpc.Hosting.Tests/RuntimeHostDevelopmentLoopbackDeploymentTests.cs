using System.Net;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Hosting.Tests;

public sealed class RuntimeHostDevelopmentLoopbackDeploymentTests
{
    [Fact]
    public void Create_NullBinding_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            () => RuntimeHostDevelopmentLoopbackDeployment.Create(
                binding: null!,
                new TestSnapshotProvider()));
    }

    [Fact]
    public void Create_NullSnapshotProvider_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            () => RuntimeHostDevelopmentLoopbackDeployment.Create(
                new LoopbackGrpcBinding(
                    IPAddress.Loopback,
                    0),
                snapshotProvider: null!));
    }

    [Fact]
    public async Task Create_LoopbackBinding_ShouldServeSnapshotWithoutTls()
    {
        var snapshotProvider =
            new TestSnapshotProvider();

        await using RuntimeHostDevelopmentLoopbackDeployment deployment =
            RuntimeHostDevelopmentLoopbackDeployment.Create(
                new LoopbackGrpcBinding(
                    IPAddress.Loopback,
                    0),
                snapshotProvider);

        await deployment.Application.StartAsync();

        try
        {
            IServer server =
                deployment.Application.Services
                    .GetRequiredService<IServer>();

            IServerAddressesFeature addressesFeature =
                server.Features.Get<IServerAddressesFeature>()
                ?? throw new InvalidOperationException(
                    "The server addresses feature is unavailable.");

            string address =
                Assert.Single(
                    addressesFeature.Addresses);

            var uri =
                new Uri(
                    address);

            Assert.Equal(
                Uri.UriSchemeHttp,
                uri.Scheme);
            Assert.True(
                IPAddress.IsLoopback(
                    IPAddress.Parse(
                        uri.Host)));

            using GrpcChannel channel =
                GrpcChannel.ForAddress(
                    uri);

            var client =
                new GrpcV1.RuntimeHostRemoteApi
                    .RuntimeHostRemoteApiClient(
                        channel);

            GrpcV1.GetSnapshotResponse response =
                await client.GetSnapshotAsync(
                    new GrpcV1.GetSnapshotRequest(),
                    deadline:
                        DateTime.UtcNow.AddSeconds(
                            10));

            Assert.Equal(
                "runtime-host-development-loopback",
                response.RuntimeHostId);
            Assert.Empty(
                response.Endpoints);
        }
        finally
        {
            await deployment.Application.StopAsync();
        }
    }

    [Fact]
    public async Task DisposeAsync_Twice_ShouldRemainIdempotent()
    {
        RuntimeHostDevelopmentLoopbackDeployment deployment =
            RuntimeHostDevelopmentLoopbackDeployment.Create(
                new LoopbackGrpcBinding(
                    IPAddress.Loopback,
                    0),
                new TestSnapshotProvider());

        await deployment.DisposeAsync();
        await deployment.DisposeAsync();
    }

    private sealed class TestSnapshotProvider
        : Northbound.IRuntimeHostSnapshotProvider
    {
        public Northbound.PublishedRuntimeHostSnapshot Capture()
        {
            return new Northbound.PublishedRuntimeHostSnapshot(
                new Northbound.RuntimeHostId(
                    "runtime-host-development-loopback"),
                Northbound.RuntimeHostApiVersion.Current,
                Array.Empty<Northbound.PublishedRuntimeEndpointSnapshot>());
        }
    }
}
