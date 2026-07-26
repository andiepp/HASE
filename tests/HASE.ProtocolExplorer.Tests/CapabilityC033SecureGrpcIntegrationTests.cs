using Grpc.Core;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.ProtocolExplorer.Scenarios;
using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;
using Xunit;

namespace Hase.ProtocolExplorer.Tests;

public sealed class CapabilityC033SecureGrpcIntegrationTests
{
    private static readonly DateTimeOffset ValidationTimeUtc =
        new(
            2026,
            7,
            26,
            15,
            30,
            0,
            TimeSpan.Zero);

    [Fact]
    public async Task StartAsync_ShouldResolveEphemeralHttpsAddress()
    {
        await using CapabilityC033SecureHostComposition composition =
            await CreateHostAsync(
                new TrackingCommandService());

        Uri address =
            await composition.StartAsync();

        Assert.Equal(
            Uri.UriSchemeHttps,
            address.Scheme);
        Assert.True(
            address.IsLoopback);
        Assert.True(
            address.Port > 0);
    }

    [Fact]
    public async Task ExecuteCommandAsync_ShouldReachSuppliedService()
    {
        var commandService =
            new TrackingCommandService();

        await using CapabilityC033SecureHostComposition composition =
            await CreateHostAsync(
                commandService);

        Uri address =
            await composition.StartAsync();

        using CapabilityC033SecureGrpcClient client =
            CapabilityC033SecureGrpcClient.Create(
                address,
                composition.AuthenticationComposition
                    .Certificates
                    .ClientCertificate,
                composition.AuthenticationComposition
                    .Certificates
                    .ServerCertificate);
        Northbound.RuntimeHostCommandTarget target =
            CreateTarget();

        GrpcV1.CommandOperationResult response =
            await client.ExecuteCommandAsync(
                target,
                argument: null,
                DateTime.UtcNow.AddSeconds(
                    10));

        Assert.Equal(
            1,
            commandService.ExecutionCount);
        Assert.Equal(
            target,
            commandService.Target);
        Assert.Null(
            commandService.Argument);
        Assert.Equal(
            GrpcV1.CommandOperationStatus.Success,
            response.Status);
        Assert.Equal(
            "completed",
            response.ReturnValue.StringValue);
    }

    [Fact]
    public async Task ExecuteCommandAsync_MissingClientCertificate_ShouldRejectBeforeService()
    {
        var commandService =
            new TrackingCommandService();

        await using CapabilityC033SecureHostComposition composition =
            await CreateHostAsync(
                commandService);

        Uri address =
            await composition.StartAsync();

        using CapabilityC033SecureGrpcClient client =
            CapabilityC033SecureGrpcClient.Create(
                address,
                clientCertificate: null,
                composition.AuthenticationComposition
                    .Certificates
                    .ServerCertificate);

        RpcException exception =
            await Assert.ThrowsAsync<RpcException>(
                () =>
                    client.ExecuteCommandAsync(
                        CreateTarget(),
                        argument: null,
                        DateTime.UtcNow.AddSeconds(
                            10)));

        Assert.Equal(
            StatusCode.Unavailable,
            exception.StatusCode);
        Assert.Equal(
            0,
            commandService.ExecutionCount);
    }

    [Fact]
    public async Task ExecuteCommandAsync_UnenrolledClientCertificate_ShouldRejectBeforeService()
    {
        var commandService =
            new TrackingCommandService();

        await using CapabilityC033SecureHostComposition composition =
            await CreateHostAsync(
                commandService);
        using CapabilityC032AuthenticationComposition unenrolledComposition =
            CapabilityC032AuthenticationComposition.Create(
                ValidationTimeUtc);

        Uri address =
            await composition.StartAsync();

        using CapabilityC033SecureGrpcClient client =
            CapabilityC033SecureGrpcClient.Create(
                address,
                unenrolledComposition
                    .Certificates
                    .ClientCertificate,
                composition.AuthenticationComposition
                    .Certificates
                    .ServerCertificate);

        RpcException exception =
            await Assert.ThrowsAsync<RpcException>(
                () =>
                    client.ExecuteCommandAsync(
                        CreateTarget(),
                        argument: null,
                        DateTime.UtcNow.AddSeconds(
                            10)));

        Assert.Equal(
            StatusCode.Unauthenticated,
            exception.StatusCode);
        Assert.Equal(
            0,
            commandService.ExecutionCount);
    }

    private static Task<CapabilityC033SecureHostComposition> CreateHostAsync(
        Northbound.IRuntimeHostCommandService commandService)
    {
        return CapabilityC033SecureHostComposition.CreateAsync(
            new TestSnapshotProvider(),
            new TestPropertyService(),
            commandService,
            ValidationTimeUtc);
    }

    private static Northbound.RuntimeHostCommandTarget CreateTarget()
    {
        return new Northbound.RuntimeHostCommandTarget(
            new EndpointId(
                "arduino-uno-01"),
            new Northbound.RuntimeEndpointAttachmentGeneration(
                new Guid(
                    "868e79d4-b1a4-4a63-81cd-5a800d9ba3fd")),
            new InstrumentId(
                "controller-01"),
            new DescriptorPath(
                "Led",
                "Toggle"));
    }

    private sealed class TestSnapshotProvider
        : Northbound.IRuntimeHostSnapshotProvider
    {
        public Northbound.PublishedRuntimeHostSnapshot Capture()
        {
            return new Northbound.PublishedRuntimeHostSnapshot(
                new Northbound.RuntimeHostId(
                    "runtime-host-c033-secure-grpc"),
                Northbound.RuntimeHostApiVersion.Current,
                Array.Empty<
                    Northbound.PublishedRuntimeEndpointSnapshot>());
        }
    }

    private sealed class TestPropertyService
        : Northbound.IRuntimeHostPropertyService
    {
        public Northbound.RuntimeHostCachedPropertyResult GetCached(
            Northbound.RuntimeHostPropertyTarget target)
        {
            throw new NotSupportedException();
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

    private sealed class TrackingCommandService
        : Northbound.IRuntimeHostCommandService
    {
        public int ExecutionCount
        {
            get;
            private set;
        }

        public Northbound.RuntimeHostCommandTarget? Target
        {
            get;
            private set;
        }

        public object? Argument
        {
            get;
            private set;
        }

        public Task<Northbound.RuntimeHostCommandOperationResult> ExecuteAsync(
            Northbound.RuntimeHostCommandTarget target,
            object? argument,
            CancellationToken cancellationToken = default)
        {
            ExecutionCount++;
            Target =
                target;
            Argument =
                argument;

            return Task.FromResult(
                Northbound.RuntimeHostCommandOperationResult.Successful(
                    "completed"));
        }
    }
}
