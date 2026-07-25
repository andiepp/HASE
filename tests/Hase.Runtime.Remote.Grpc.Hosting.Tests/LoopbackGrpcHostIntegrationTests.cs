using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Grpc.Core;
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
    public async Task GetSnapshot_Ipv6LoopbackHttp2_ShouldReturnAuthoritativeSnapshot()
    {
        if (!Socket.OSSupportsIPv6)
        {
            return;
        }

        var snapshotProvider =
            new TestSnapshotProvider();

        await using WebApplication application =
            LoopbackGrpcHostFactory.Create(
                new LoopbackGrpcBinding(
                    IPAddress.IPv6Loopback,
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
            var uri =
                new Uri(
                    Assert.Single(
                        addressesFeature.Addresses));
            IPAddress address =
                IPAddress.Parse(
                    uri.Host.Trim(
                        '[',
                        ']'));

            Assert.Equal(
                Uri.UriSchemeHttp,
                uri.Scheme);
            Assert.True(
                IPAddress.IsLoopback(
                    address));
            Assert.Equal(
                AddressFamily.InterNetworkV6,
                address.AddressFamily);

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

    [Fact]
    public async Task Observe_Ipv4LoopbackHttp2_ShouldWriteInitialSnapshotFirst()
    {
        var observationService =
            new TestObservationService();

        await using WebApplication application =
            LoopbackGrpcHostFactory.Create(
                new LoopbackGrpcBinding(
                    IPAddress.Loopback,
                    0),
                new TestSnapshotProvider(),
                propertyService: null,
                commandService: null,
                observationService:
                    observationService);

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

            using AsyncServerStreamingCall<GrpcV1.ObserveResponse> call =
                client.Observe(
                    new GrpcV1.ObserveRequest(),
                    deadline:
                        DateTime.UtcNow.AddSeconds(
                            10));
            var messages =
                new List<GrpcV1.ObserveResponse>();

            while (await call.ResponseStream.MoveNext())
            {
                messages.Add(
                    call.ResponseStream.Current);
            }

            Assert.Collection(
                messages,
                initial =>
                {
                    Assert.Equal(
                        GrpcV1.ObserveResponse.ContentOneofCase.InitialSnapshot,
                        initial.ContentCase);
                    Assert.Equal(
                        "runtime-host-observation-loopback",
                        initial.InitialSnapshot.Snapshot.RuntimeHostId);
                    Assert.Equal(
                        0UL,
                        initial.InitialSnapshot.SnapshotSequence);
                },
                observation =>
                {
                    Assert.Equal(
                        GrpcV1.ObserveResponse.ContentOneofCase.Observation,
                        observation.ContentCase);
                    Assert.Equal(
                        1UL,
                        observation.Observation.Sequence);
                    Assert.Equal(
                        GrpcV1.RuntimeHostObservationKind.EventOccurred,
                        observation.Observation.Kind);
                    Assert.Equal(
                        "pressed",
                        observation.Observation.EventOccurred.Value.StringValue);
                });
            Assert.Equal(
                1,
                observationService.OpenCount);
            Assert.True(
                observationService.CancellationToken.CanBeCanceled);
            Assert.Equal(
                1,
                observationService.Subscription.DisposeCount);
        }
        finally
        {
            await application.StopAsync();
        }
    }

    [Fact]
    public async Task Observe_ClientCancellation_ShouldDisposeOnlyItsSubscription()
    {
        var observationService =
            new TestBlockingObservationService();

        await using WebApplication application =
            LoopbackGrpcHostFactory.Create(
                new LoopbackGrpcBinding(
                    IPAddress.Loopback,
                    0),
                new TestSnapshotProvider(),
                propertyService: null,
                commandService: null,
                observationService:
                    observationService);

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
            using var cancellationSource =
                new CancellationTokenSource();
            using AsyncServerStreamingCall<GrpcV1.ObserveResponse> call =
                client.Observe(
                    new GrpcV1.ObserveRequest(),
                    cancellationToken:
                        cancellationSource.Token);

            Assert.True(
                await call.ResponseStream.MoveNext(
                    CancellationToken.None));
            Assert.Equal(
                GrpcV1.ObserveResponse.ContentOneofCase.InitialSnapshot,
                call.ResponseStream.Current.ContentCase);

            cancellationSource.Cancel();

            RpcException exception =
                await Assert.ThrowsAsync<RpcException>(
                    () =>
                        call.ResponseStream.MoveNext(
                            CancellationToken.None));

            Assert.Equal(
                StatusCode.Cancelled,
                exception.StatusCode);

            await WaitUntilAsync(
                () =>
                    observationService.Subscription.DisposeCount
                    == 1);

            Assert.Equal(
                1,
                observationService.OpenCount);
            Assert.True(
                observationService.CancellationToken.CanBeCanceled);
            Assert.Equal(
                1,
                observationService.Subscription.DisposeCount);
        }
        finally
        {
            await application.StopAsync();
        }
    }

    [Fact]
    public async Task Observe_DeadlineExceeded_ShouldDisposeSubscription()
    {
        var observationService =
            new TestBlockingObservationService();

        await using WebApplication application =
            LoopbackGrpcHostFactory.Create(
                new LoopbackGrpcBinding(
                    IPAddress.Loopback,
                    0),
                new TestSnapshotProvider(),
                propertyService: null,
                commandService: null,
                observationService:
                    observationService);

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
            using AsyncServerStreamingCall<GrpcV1.ObserveResponse> call =
                client.Observe(
                    new GrpcV1.ObserveRequest(),
                    deadline:
                        DateTime.UtcNow.AddSeconds(
                            1));

            Assert.True(
                await call.ResponseStream.MoveNext(
                    CancellationToken.None));
            Assert.Equal(
                GrpcV1.ObserveResponse.ContentOneofCase.InitialSnapshot,
                call.ResponseStream.Current.ContentCase);

            RpcException exception =
                await Assert.ThrowsAsync<RpcException>(
                    () =>
                        call.ResponseStream.MoveNext(
                            CancellationToken.None));

            Assert.Equal(
                StatusCode.DeadlineExceeded,
                exception.StatusCode);

            await WaitUntilAsync(
                () =>
                    observationService.Subscription.DisposeCount
                    == 1);

            Assert.Equal(
                1,
                observationService.OpenCount);
            Assert.Equal(
                1,
                observationService.Subscription.DisposeCount);
        }
        finally
        {
            await application.StopAsync();
        }
    }

    [Fact]
    public async Task Observe_HostShutdown_ShouldDisposeSubscriptionBeforeStopping()
    {
        var observationService =
            new TestBlockingObservationService();

        await using WebApplication application =
            LoopbackGrpcHostFactory.Create(
                new LoopbackGrpcBinding(
                    IPAddress.Loopback,
                    0),
                new TestSnapshotProvider(),
                propertyService: null,
                commandService: null,
                observationService:
                    observationService);

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
            using AsyncServerStreamingCall<GrpcV1.ObserveResponse> call =
                client.Observe(
                    new GrpcV1.ObserveRequest());

            Assert.True(
                await call.ResponseStream.MoveNext(
                    CancellationToken.None));
            Assert.Equal(
                GrpcV1.ObserveResponse.ContentOneofCase.InitialSnapshot,
                call.ResponseStream.Current.ContentCase);

            await application
                .StopAsync()
                .WaitAsync(
                    TimeSpan.FromSeconds(
                        2));

            Assert.Equal(
                1,
                observationService.Subscription.DisposeCount);

            await Assert.ThrowsAsync<RpcException>(
                () =>
                    call.ResponseStream.MoveNext(
                        CancellationToken.None));

            Assert.Equal(
                1,
                observationService.OpenCount);
            Assert.Equal(
                1,
                observationService.Subscription.DisposeCount);
        }
        finally
        {
            await application.StopAsync();
        }
    }

    [Fact]
    public async Task Observe_CancellingOneClient_ShouldNotAffectAnotherSubscription()
    {
        var observationService =
            new TestIndependentObservationService();

        await using WebApplication application =
            LoopbackGrpcHostFactory.Create(
                new LoopbackGrpcBinding(
                    IPAddress.Loopback,
                    0),
                new TestSnapshotProvider(),
                propertyService: null,
                commandService: null,
                observationService:
                    observationService);

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
            using var firstCancellation =
                new CancellationTokenSource();
            using var secondCancellation =
                new CancellationTokenSource();
            using AsyncServerStreamingCall<GrpcV1.ObserveResponse> firstCall =
                client.Observe(
                    new GrpcV1.ObserveRequest(),
                    cancellationToken:
                        firstCancellation.Token);
            using AsyncServerStreamingCall<GrpcV1.ObserveResponse> secondCall =
                client.Observe(
                    new GrpcV1.ObserveRequest(),
                    cancellationToken:
                        secondCancellation.Token);

            Assert.True(
                await firstCall.ResponseStream.MoveNext(
                    CancellationToken.None));
            Assert.True(
                await secondCall.ResponseStream.MoveNext(
                    CancellationToken.None));

            await WaitUntilAsync(
                () =>
                    observationService.Subscriptions.Count
                    == 2);

            firstCancellation.Cancel();

            RpcException firstException =
                await Assert.ThrowsAsync<RpcException>(
                    () =>
                        firstCall.ResponseStream.MoveNext(
                            CancellationToken.None));

            Assert.Equal(
                StatusCode.Cancelled,
                firstException.StatusCode);

            await WaitUntilAsync(
                () =>
                    observationService.Subscriptions[0].DisposeCount
                    == 1);

            Assert.Equal(
                0,
                observationService.Subscriptions[1].DisposeCount);

            secondCancellation.Cancel();

            RpcException secondException =
                await Assert.ThrowsAsync<RpcException>(
                    () =>
                        secondCall.ResponseStream.MoveNext(
                            CancellationToken.None));

            Assert.Equal(
                StatusCode.Cancelled,
                secondException.StatusCode);

            await WaitUntilAsync(
                () =>
                    observationService.Subscriptions[1].DisposeCount
                    == 1);

            Assert.Equal(
                1,
                observationService.Subscriptions[0].DisposeCount);
            Assert.Equal(
                1,
                observationService.Subscriptions[1].DisposeCount);
        }
        finally
        {
            await application.StopAsync();
        }
    }

    private static async Task WaitUntilAsync(
        Func<bool> condition)
    {
        using var timeout =
            new CancellationTokenSource(
                TimeSpan.FromSeconds(
                    2));

        while (!condition())
        {
            await Task.Delay(
                TimeSpan.FromMilliseconds(
                    10),
                timeout.Token);
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

    private sealed class TestObservationService
        : Northbound.IRuntimeHostObservationService
    {
        public TestObservationService()
        {
            var snapshot =
                new Northbound.PublishedRuntimeHostSnapshot(
                    new Northbound.RuntimeHostId(
                        "runtime-host-observation-loopback"),
                    Northbound.RuntimeHostApiVersion.Current,
                    Array.Empty<Northbound.PublishedRuntimeEndpointSnapshot>());
            var observation =
                new Northbound.RuntimeHostObservation(
                    new Northbound.RuntimeHostObservationSequence(
                        1),
                    new Hase.Core.Domain.Identity.EndpointId(
                        "endpoint-01"),
                    new Northbound.RuntimeEndpointAttachmentGeneration(
                        new Guid(
                            "868e79d4-b1a4-4a63-81cd-5a800d9ba3fd")),
                    new Northbound.RuntimeHostEventOccurredObservationPayload(
                        new Hase.Core.Domain.Identity.InstrumentId(
                            "controller-01"),
                        new Hase.Core.Domain.Properties.DescriptorPath(
                            "Controller",
                            "ButtonPressed"),
                        DateTimeOffset.UnixEpoch,
                        "pressed"));

            Subscription =
                new TestObservationSubscription(
                    snapshot,
                    observation);
        }

        public int OpenCount
        {
            get;
            private set;
        }

        public CancellationToken CancellationToken
        {
            get;
            private set;
        }

        public TestObservationSubscription Subscription
        {
            get;
        }

        public Task<Northbound.RuntimeHostObservationSubscription>
            OpenSubscriptionAsync(
                Northbound.RuntimeHostObservationSubscriptionOptions options,
                CancellationToken cancellationToken = default)
        {
            OpenCount++;
            CancellationToken =
                cancellationToken;

            return Task.FromResult<Northbound.RuntimeHostObservationSubscription>(
                Subscription);
        }
    }

    private sealed class TestObservationSubscription
        : Northbound.RuntimeHostObservationSubscription
    {
        private readonly Northbound.RuntimeHostObservation observation;

        public TestObservationSubscription(
            Northbound.PublishedRuntimeHostSnapshot initialSnapshot,
            Northbound.RuntimeHostObservation observation)
            : base(
                initialSnapshot,
                new Northbound.RuntimeHostObservationSequence(
                    0))
        {
            this.observation =
                observation;
        }

        public int DisposeCount
        {
            get;
            private set;
        }

        public override async IAsyncEnumerable<Northbound.RuntimeHostObservation>
            ReadAllAsync(
                [EnumeratorCancellation]
                CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            yield return observation;

            await Task.CompletedTask;
        }

        public override ValueTask DisposeAsync()
        {
            DisposeCount++;

            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestBlockingObservationService
        : Northbound.IRuntimeHostObservationService
    {
        public TestBlockingObservationService()
        {
            Subscription =
                new TestBlockingObservationSubscription(
                    new Northbound.PublishedRuntimeHostSnapshot(
                        new Northbound.RuntimeHostId(
                            "runtime-host-observation-cancellation"),
                        Northbound.RuntimeHostApiVersion.Current,
                        Array.Empty<
                            Northbound.PublishedRuntimeEndpointSnapshot>()));
        }

        public int OpenCount
        {
            get;
            private set;
        }

        public CancellationToken CancellationToken
        {
            get;
            private set;
        }

        public TestBlockingObservationSubscription Subscription
        {
            get;
        }

        public Task<Northbound.RuntimeHostObservationSubscription>
            OpenSubscriptionAsync(
                Northbound.RuntimeHostObservationSubscriptionOptions options,
                CancellationToken cancellationToken = default)
        {
            OpenCount++;
            CancellationToken =
                cancellationToken;

            return Task.FromResult<Northbound.RuntimeHostObservationSubscription>(
                Subscription);
        }
    }

    private sealed class TestBlockingObservationSubscription
        : Northbound.RuntimeHostObservationSubscription
    {
        public TestBlockingObservationSubscription(
            Northbound.PublishedRuntimeHostSnapshot initialSnapshot)
            : base(
                initialSnapshot,
                new Northbound.RuntimeHostObservationSequence(
                    0))
        {
        }

        public int DisposeCount
        {
            get;
            private set;
        }

        public override async IAsyncEnumerable<Northbound.RuntimeHostObservation>
            ReadAllAsync(
                [EnumeratorCancellation]
                CancellationToken cancellationToken = default)
        {
            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                cancellationToken);

            yield break;
        }

        public override ValueTask DisposeAsync()
        {
            DisposeCount++;

            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestIndependentObservationService
        : Northbound.IRuntimeHostObservationService
    {
        private readonly object syncRoot =
            new();

        public List<TestBlockingObservationSubscription> Subscriptions
        {
            get;
        } =
            [];

        public Task<Northbound.RuntimeHostObservationSubscription>
            OpenSubscriptionAsync(
                Northbound.RuntimeHostObservationSubscriptionOptions options,
                CancellationToken cancellationToken = default)
        {
            TestBlockingObservationSubscription subscription;

            lock (syncRoot)
            {
                subscription =
                    new TestBlockingObservationSubscription(
                        new Northbound.PublishedRuntimeHostSnapshot(
                            new Northbound.RuntimeHostId(
                                $"runtime-host-observation-"
                                + $"{Subscriptions.Count + 1}"),
                            Northbound.RuntimeHostApiVersion.Current,
                            Array.Empty<
                                Northbound
                                    .PublishedRuntimeEndpointSnapshot>()));

                Subscriptions.Add(
                    subscription);
            }

            return Task.FromResult<Northbound.RuntimeHostObservationSubscription>(
                subscription);
        }
    }
}
