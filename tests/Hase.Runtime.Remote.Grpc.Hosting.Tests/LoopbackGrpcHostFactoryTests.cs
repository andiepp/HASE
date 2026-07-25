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
    public void Create_NullPropertyService_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "propertyService",
            () =>
                LoopbackGrpcHostFactory.Create(
                    new LoopbackGrpcBinding(
                        IPAddress.Loopback,
                        0),
                    new TestSnapshotProvider(),
                    null!));
    }

    [Fact]
    public void Create_NullCommandService_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "commandService",
            () =>
                LoopbackGrpcHostFactory.Create(
                    new LoopbackGrpcBinding(
                        IPAddress.Loopback,
                        0),
                    new TestSnapshotProvider(),
                    propertyService: null,
                    commandService: null!));
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

    [Fact]
    public async Task Create_PropertyDependencies_ShouldRegisterMapperRoots()
    {
        var propertyService =
            new TestPropertyService();

        await using WebApplication application =
            LoopbackGrpcHostFactory.Create(
                new LoopbackGrpcBinding(
                    IPAddress.Loopback,
                    0),
                new TestSnapshotProvider(),
                propertyService);

        Assert.Same(
            propertyService,
            application.Services.GetRequiredService<
                Northbound.IRuntimeHostPropertyService>());
        Assert.NotNull(
            application.Services.GetRequiredService<
                IRuntimeHostPropertyTargetMapper>());
        Assert.NotNull(
            application.Services.GetRequiredService<
                IRuntimeHostCachedPropertyResultMapper>());
        Assert.NotNull(
            application.Services.GetRequiredService<
                IRuntimeHostPropertyOperationResultMapper>());
        Assert.NotNull(
            application.Services.GetRequiredService<
                IRemoteValueMapper>());
    }

    [Fact]
    public async Task Create_CommandDependencies_ShouldRegisterMapperRoots()
    {
        var commandService =
            new TestCommandService();

        await using WebApplication application =
            LoopbackGrpcHostFactory.Create(
                new LoopbackGrpcBinding(
                    IPAddress.Loopback,
                    0),
                new TestSnapshotProvider(),
                propertyService: null,
                commandService:
                    commandService);

        Assert.Same(
            commandService,
            application.Services.GetRequiredService<
                Northbound.IRuntimeHostCommandService>());
        Assert.NotNull(
            application.Services.GetRequiredService<
                IRuntimeHostCommandTargetMapper>());
        Assert.NotNull(
            application.Services.GetRequiredService<
                IRuntimeHostCommandOperationResultMapper>());
        Assert.NotNull(
            application.Services.GetRequiredService<
                IRemoteValueMapper>());
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

    private sealed class TestPropertyService
        : Northbound.IRuntimeHostPropertyService
    {
        public Northbound.RuntimeHostCachedPropertyResult GetCached(
            Northbound.RuntimeHostPropertyTarget target)
        {
            return Northbound.RuntimeHostCachedPropertyResult.Failed(
                Northbound.RuntimeHostPropertyOperationStatus.PropertyNotFound);
        }

        public Task<Northbound.RuntimeHostPropertyOperationResult> ReadAsync(
            Northbound.RuntimeHostPropertyTarget target,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Northbound.RuntimeHostPropertyOperationResult> WriteAsync(
            Northbound.RuntimeHostPropertyTarget target,
            object? requestedValue,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class TestCommandService
        : Northbound.IRuntimeHostCommandService
    {
        public Task<Northbound.RuntimeHostCommandOperationResult> ExecuteAsync(
            Northbound.RuntimeHostCommandTarget target,
            object? argument,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
