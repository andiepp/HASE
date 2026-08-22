using System.Net;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Hosting.Tests;

public sealed class RuntimeHostDevelopmentLoopbackClientIntegrationTests
{
    [Fact]
    public async Task GetSnapshot_DevelopmentClientAgainstDevelopmentHost_ShouldSucceedWithoutTls()
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

            var boundAddress =
                new Uri(
                    Assert.Single(
                        addressesFeature.Addresses));

            var options =
                new RuntimeHostDevelopmentLoopbackClientOptions(
                    new Uri(
                        $"http://127.0.0.1:{boundAddress.Port}"));

            using RuntimeHostDevelopmentLoopbackGrpcClient client =
                RuntimeHostDevelopmentLoopbackGrpcClient.Create(
                    options);

            GrpcV1.GetSnapshotResponse response =
                await client.Client.GetSnapshotAsync(
                    new GrpcV1.GetSnapshotRequest(),
                    deadline:
                        DateTime.UtcNow.AddSeconds(
                            10));

            Assert.Equal(
                "runtime-host-development-client-integration",
                response.RuntimeHostId);
            Assert.Empty(
                response.Endpoints);
        }
        finally
        {
            await deployment.Application.StopAsync();
        }
    }

    private sealed class TestSnapshotProvider
        : Northbound.IRuntimeHostSnapshotProvider
    {
        public Northbound.PublishedRuntimeHostSnapshot Capture()
        {
            return new Northbound.PublishedRuntimeHostSnapshot(
                new Northbound.RuntimeHostId(
                    "runtime-host-development-client-integration"),
                Northbound.RuntimeHostApiVersion.Current,
                Array.Empty<Northbound.PublishedRuntimeEndpointSnapshot>());
        }
    }
}
