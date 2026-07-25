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

    [Fact]
    public async Task ReadCachedProperty_Ipv4LoopbackHttp2_ShouldReachNorthboundService()
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

        await application.StartAsync();

        try
        {
            IServer server =
                application.Services.GetRequiredService<IServer>();
            IServerAddressesFeature addressesFeature =
                server.Features.Get<IServerAddressesFeature>()
                ?? throw new InvalidOperationException(
                    "The server addresses feature is unavailable.");
            var uri =
                new Uri(
                    Assert.Single(
                        addressesFeature.Addresses));

            using GrpcChannel channel =
                GrpcChannel.ForAddress(
                    uri);
            var client =
                new GrpcV1.RuntimeHostRemoteApi
                    .RuntimeHostRemoteApiClient(
                        channel);
            var generation =
                new Guid(
                    "868e79d4-b1a4-4a63-81cd-5a800d9ba3fd");

            GrpcV1.CachedPropertyResult response =
                await client.ReadCachedPropertyAsync(
                    new GrpcV1.ReadCachedPropertyRequest
                    {
                        Target =
                            new GrpcV1.PropertyTarget
                            {
                                EndpointId =
                                    "endpoint-01",
                                AttachmentGeneration =
                                    generation.ToString(
                                        "D"),
                                InstrumentId =
                                    "environment-sensor-01",
                                PropertyId =
                                    "temperature"
                            }
                    },
                    deadline:
                        DateTime.UtcNow.AddSeconds(
                            10));

            Assert.Equal(
                GrpcV1.PropertyOperationStatus.PropertyNotFound,
                response.Status);
            Assert.Equal(
                "Property not found.",
                response.Diagnostic);
            Assert.NotNull(
                propertyService.CachedTarget);
            Assert.Equal(
                "endpoint-01",
                propertyService.CachedTarget.EndpointId.Value);
            Assert.Equal(
                generation,
                propertyService.CachedTarget.AttachmentGeneration.Value);
            Assert.Equal(
                "environment-sensor-01",
                propertyService.CachedTarget.InstrumentId.Value);
            Assert.Equal(
                "temperature",
                propertyService.CachedTarget.PropertyId.Value);
        }
        finally
        {
            await application.StopAsync();
        }
    }

    [Fact]
    public async Task ReadAuthoritativeProperty_Ipv4LoopbackHttp2_ShouldReturnConfirmedValue()
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

        await application.StartAsync();

        try
        {
            IServer server =
                application.Services.GetRequiredService<IServer>();
            IServerAddressesFeature addressesFeature =
                server.Features.Get<IServerAddressesFeature>()
                ?? throw new InvalidOperationException(
                    "The server addresses feature is unavailable.");
            var uri =
                new Uri(
                    Assert.Single(
                        addressesFeature.Addresses));

            using GrpcChannel channel =
                GrpcChannel.ForAddress(
                    uri);
            var client =
                new GrpcV1.RuntimeHostRemoteApi
                    .RuntimeHostRemoteApiClient(
                        channel);
            var generation =
                new Guid(
                    "868e79d4-b1a4-4a63-81cd-5a800d9ba3fd");

            GrpcV1.PropertyOperationResult response =
                await client.ReadAuthoritativePropertyAsync(
                    new GrpcV1.ReadAuthoritativePropertyRequest
                    {
                        Target =
                            new GrpcV1.PropertyTarget
                            {
                                EndpointId =
                                    "endpoint-01",
                                AttachmentGeneration =
                                    generation.ToString(
                                        "D"),
                                InstrumentId =
                                    "environment-sensor-01",
                                PropertyId =
                                    "temperature"
                            }
                    },
                    deadline:
                        DateTime.UtcNow.AddSeconds(
                            10));

            Assert.Equal(
                GrpcV1.PropertyOperationStatus.Success,
                response.Status);
            Assert.Equal(
                23.75,
                response.ConfirmedValue.Value.NumericValue);
            Assert.Equal(
                GrpcV1.PropertyQuality.Good,
                response.ConfirmedValue.Quality);
            Assert.NotNull(
                propertyService.ReadTarget);
            Assert.Equal(
                generation,
                propertyService.ReadTarget.AttachmentGeneration.Value);
            Assert.True(
                propertyService.ReadCancellationToken.CanBeCanceled);
        }
        finally
        {
            await application.StopAsync();
        }
    }

    [Fact]
    public async Task WriteProperty_Ipv4LoopbackHttp2_ShouldReachNorthboundService()
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

        await application.StartAsync();

        try
        {
            IServer server =
                application.Services.GetRequiredService<IServer>();
            IServerAddressesFeature addressesFeature =
                server.Features.Get<IServerAddressesFeature>()
                ?? throw new InvalidOperationException(
                    "The server addresses feature is unavailable.");
            var uri =
                new Uri(
                    Assert.Single(
                        addressesFeature.Addresses));

            using GrpcChannel channel =
                GrpcChannel.ForAddress(
                    uri);
            var client =
                new GrpcV1.RuntimeHostRemoteApi
                    .RuntimeHostRemoteApiClient(
                        channel);
            var generation =
                new Guid(
                    "868e79d4-b1a4-4a63-81cd-5a800d9ba3fd");

            GrpcV1.PropertyOperationResult response =
                await client.WritePropertyAsync(
                    new GrpcV1.WritePropertyRequest
                    {
                        Target =
                            new GrpcV1.PropertyTarget
                            {
                                EndpointId =
                                    "endpoint-01",
                                AttachmentGeneration =
                                    generation.ToString(
                                        "D"),
                                InstrumentId =
                                    "environment-sensor-01",
                                PropertyId =
                                    "enabled"
                            },
                        RequestedValue =
                            new GrpcV1.RemoteValue
                            {
                                BooleanValue =
                                    true
                            }
                    },
                    deadline:
                        DateTime.UtcNow.AddSeconds(
                            10));

            Assert.Equal(
                GrpcV1.PropertyOperationStatus.Success,
                response.Status);
            Assert.True(
                response.ConfirmedValue.Value.BooleanValue);
            Assert.NotNull(
                propertyService.WriteTarget);
            Assert.Equal(
                generation,
                propertyService.WriteTarget.AttachmentGeneration.Value);
            Assert.Equal(
                true,
                propertyService.RequestedValue);
            Assert.True(
                propertyService.WriteCancellationToken.CanBeCanceled);
        }
        finally
        {
            await application.StopAsync();
        }
    }

    [Fact]
    public async Task ExecuteCommand_Ipv4LoopbackHttp2_ShouldReachNorthboundServiceOnce()
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

        await application.StartAsync();

        try
        {
            IServer server =
                application.Services.GetRequiredService<IServer>();
            IServerAddressesFeature addressesFeature =
                server.Features.Get<IServerAddressesFeature>()
                ?? throw new InvalidOperationException(
                    "The server addresses feature is unavailable.");
            var uri =
                new Uri(
                    Assert.Single(
                        addressesFeature.Addresses));

            using GrpcChannel channel =
                GrpcChannel.ForAddress(
                    uri);
            var client =
                new GrpcV1.RuntimeHostRemoteApi
                    .RuntimeHostRemoteApiClient(
                        channel);
            var generation =
                new Guid(
                    "868e79d4-b1a4-4a63-81cd-5a800d9ba3fd");
            var target =
                new GrpcV1.CommandTarget
                {
                    EndpointId =
                        "endpoint-01",
                    AttachmentGeneration =
                        generation.ToString(
                            "D"),
                    InstrumentId =
                        "environment-sensor-01"
                };

            target.CommandPathSegments.Add(
                "Calibration");
            target.CommandPathSegments.Add(
                "Reset");

            GrpcV1.CommandOperationResult response =
                await client.ExecuteCommandAsync(
                    new GrpcV1.ExecuteCommandRequest
                    {
                        Target =
                            target,
                        Argument =
                            new GrpcV1.RemoteValue
                            {
                                BooleanValue =
                                    true
                            }
                    },
                    deadline:
                        DateTime.UtcNow.AddSeconds(
                            10));

            Assert.Equal(
                GrpcV1.CommandOperationStatus.Success,
                response.Status);
            Assert.Equal(
                "completed",
                response.ReturnValue.StringValue);
            Assert.Equal(
                1,
                commandService.ExecutionCount);
            Assert.NotNull(
                commandService.Target);
            Assert.Equal(
                generation,
                commandService.Target.AttachmentGeneration.Value);
            Assert.Equal(
                new[]
                {
                    "Calibration",
                    "Reset"
                },
                commandService.Target.CommandPath.Segments);
            Assert.Equal(
                true,
                commandService.Argument);
            Assert.True(
                commandService.CancellationToken.CanBeCanceled);
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

    private sealed class TestPropertyService
        : Northbound.IRuntimeHostPropertyService
    {
        public Northbound.RuntimeHostPropertyTarget? CachedTarget
        {
            get;
            private set;
        }

        public Northbound.RuntimeHostPropertyTarget? ReadTarget
        {
            get;
            private set;
        }

        public CancellationToken ReadCancellationToken
        {
            get;
            private set;
        }

        public Northbound.RuntimeHostPropertyTarget? WriteTarget
        {
            get;
            private set;
        }

        public object? RequestedValue
        {
            get;
            private set;
        }

        public CancellationToken WriteCancellationToken
        {
            get;
            private set;
        }

        public Northbound.RuntimeHostCachedPropertyResult GetCached(
            Northbound.RuntimeHostPropertyTarget target)
        {
            CachedTarget =
                target;

            return Northbound.RuntimeHostCachedPropertyResult.Failed(
                Northbound.RuntimeHostPropertyOperationStatus.PropertyNotFound,
                "Property not found.");
        }

        public Task<Northbound.RuntimeHostPropertyOperationResult> ReadAsync(
            Northbound.RuntimeHostPropertyTarget target,
            CancellationToken cancellationToken = default)
        {
            ReadTarget =
                target;
            ReadCancellationToken =
                cancellationToken;

            return Task.FromResult(
                Northbound.RuntimeHostPropertyOperationResult.Successful(
                    new Hase.Core.Domain.Properties.PropertyValue(
                        23.75,
                        DateTimeOffset.UnixEpoch)));
        }

        public Task<Northbound.RuntimeHostPropertyOperationResult> WriteAsync(
            Northbound.RuntimeHostPropertyTarget target,
            object? requestedValue,
            CancellationToken cancellationToken = default)
        {
            WriteTarget =
                target;
            RequestedValue =
                requestedValue;
            WriteCancellationToken =
                cancellationToken;

            return Task.FromResult(
                Northbound.RuntimeHostPropertyOperationResult.Successful(
                    new Hase.Core.Domain.Properties.PropertyValue(
                        requestedValue,
                        DateTimeOffset.UnixEpoch)));
        }
    }

    private sealed class TestCommandService
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

        public CancellationToken CancellationToken
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
            CancellationToken =
                cancellationToken;

            return Task.FromResult(
                Northbound.RuntimeHostCommandOperationResult.Successful(
                    "completed"));
        }
    }
}
