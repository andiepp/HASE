using System.Net;
using Hase.Runtime.Remote.Grpc.Adapter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Northbound = global::Hase.Runtime.Northbound;

namespace Hase.Runtime.Remote.Grpc.Hosting.Tests;

public sealed class LoopbackGrpcHostFactoryTests
{
    [Fact]
    public void Create_NullBinding_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "binding",
            () =>
                LoopbackGrpcHostFactory.Create(
                    null!,
                    new TestSnapshotProvider()));
    }

    [Fact]
    public void Create_NullSnapshotProvider_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "snapshotProvider",
            () =>
                LoopbackGrpcHostFactory.Create(
                    new LoopbackGrpcBinding(
                        IPAddress.Loopback,
                        0),
                    null!));
    }

    [Fact]
    public async Task Create_ValidDependencies_ShouldRegisterAndMapService()
    {
        var snapshotProvider =
            new TestSnapshotProvider();

        await using WebApplication application =
            LoopbackGrpcHostFactory.Create(
                new LoopbackGrpcBinding(
                    IPAddress.Loopback,
                    0),
                snapshotProvider);

        Assert.Same(
            snapshotProvider,
            application.Services.GetRequiredService<
                Northbound.IRuntimeHostSnapshotProvider>());
        Assert.NotNull(
            application.Services.GetRequiredService<
                RuntimeHostSnapshotMapper>());

        IEndpointRouteBuilder routeBuilder =
            application;

        Assert.NotEmpty(
            routeBuilder.DataSources);
    }

    private sealed class TestSnapshotProvider
        : Northbound.IRuntimeHostSnapshotProvider
    {
        public Northbound.PublishedRuntimeHostSnapshot Capture()
        {
            return new Northbound.PublishedRuntimeHostSnapshot(
                new Northbound.RuntimeHostId(
                    "runtime-host-1"),
                Northbound.RuntimeHostApiVersion.Current,
                Array.Empty<Northbound.PublishedRuntimeEndpointSnapshot>());
        }
    }
}
