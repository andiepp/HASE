using System.Net;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Hosting.Tests;

public sealed class LoopbackGrpcHostIntegrationTests
{
    [Fact]
    public async Task GetSnapshot_Ipv4LoopbackHttp2_ShouldReturnAuthoritativeSnapshot()
    {
        var snapshotProvider =
            new TestSnapshotProvider();

        await using WebApplication application =
            LoopbackGrpcHostFactory.Create(
                new LoopbackGrpcBinding(
                    IPAddress.Loopback,
                    0),
                snapshotProvider);

        await application.StartAsync();

        try
        {
            IServer server =
                application.Services.GetRequiredService<IServer>();

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
            Assert.True(
                uri.Port > 0);

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
                1,
                snapshotProvider.CaptureCount);
            Assert.Equal(
                "runtime-host-loopback-integration",
                response.RuntimeHostId);
            Assert.Equal(
                1U,
                response.ApiVersion.Major);
            Assert.Equal(
                0U,
                response.ApiVersion.Minor);
            Assert.Empty(
                response.Endpoints);
        }
        finally
        {
            await application.StopAsync();
        }
    }

    private sealed class TestSnapshotProvider
        : Northbound.IRuntimeHostSnapshotProvider
    {
        public int CaptureCount
        {
            get;
            private set;
        }

        public Northbound.PublishedRuntimeHostSnapshot Capture()
        {
            CaptureCount++;

            return new Northbound.PublishedRuntimeHostSnapshot(
                new Northbound.RuntimeHostId(
                    "runtime-host-loopback-integration"),
                Northbound.RuntimeHostApiVersion.Current,
                Array.Empty<Northbound.PublishedRuntimeEndpointSnapshot>());
        }
    }
}
