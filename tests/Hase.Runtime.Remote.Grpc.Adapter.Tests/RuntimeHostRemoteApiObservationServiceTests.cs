using System.Runtime.CompilerServices;
using Grpc.Core;
using Hase.Core.Domain.Identity;
using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostRemoteApiObservationServiceTests
{
    [Fact]
    public void Constructor_IncompleteObservationDependencies_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "observationService",
            () =>
                CreateService(
                    observationService: null,
                    new TestInitialMapper(
                        new GrpcV1.ObserveResponse()),
                    new TestObservationMapper()));
        Assert.Throws<ArgumentNullException>(
            "initialSnapshotMapper",
            () =>
                CreateService(
                    new TestObservationService(
                        CreateSubscription()),
                    initialSnapshotMapper: null,
                    new TestObservationMapper()));
        Assert.Throws<ArgumentNullException>(
            "observationMapper",
            () =>
                CreateService(
                    new TestObservationService(
                        CreateSubscription()),
                    new TestInitialMapper(
                        new GrpcV1.ObserveResponse()),
                    observationMapper: null));
    }

    [Fact]
    public async Task Observe_NullRequest_ShouldThrow()
    {
        RuntimeHostRemoteApiService service =
            CreateConfiguredService(
                new TestObservationService(
                    CreateSubscription()),
                new TestInitialMapper(
                    new GrpcV1.ObserveResponse()),
                new TestObservationMapper());

        await Assert.ThrowsAsync<ArgumentNullException>(
            "request",
            () =>
                service.Observe(
                    null!,
                    new TestStreamWriter(),
                    null!));
    }

    [Fact]
    public async Task Observe_NotConfigured_ShouldThrow()
    {
        var service =
            new RuntimeHostRemoteApiService(
                new TestSnapshotProvider(),
                RuntimeHostSnapshotMapperFactory.Create());

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    service.Observe(
                        new GrpcV1.ObserveRequest(),
                        new TestStreamWriter(),
                        null!));

        Assert.Equal(
            "Runtime-host observation is not configured.",
            exception.Message);
    }

    [Fact]
    public async Task Observe_ShouldWriteInitialThenOrderedObservationsAndDispose()
    {
        TestObservationSubscription subscription =
            CreateSubscription(
                CreateObservation(
                    1),
                CreateObservation(
                    2));
        var observationService =
            new TestObservationService(
                subscription);
        var initialMessage =
            new GrpcV1.ObserveResponse
            {
                InitialSnapshot =
                    new GrpcV1.ObservationInitialSnapshot()
            };
        var initialMapper =
            new TestInitialMapper(
                initialMessage);
        var observationMapper =
            new TestObservationMapper();
        var stream =
            new TestStreamWriter();
        RuntimeHostRemoteApiService service =
            CreateConfiguredService(
                observationService,
                initialMapper,
                observationMapper);

        await service.Observe(
            new GrpcV1.ObserveRequest(),
            stream,
            null!);

        Assert.Equal(
            1,
            observationService.OpenCount);
        Assert.Equal(
            Northbound.RuntimeHostObservationSubscriptionOptions
                .DefaultBufferCapacity,
            observationService.Options!.BufferCapacity);
        Assert.Equal(
            CancellationToken.None,
            observationService.CancellationToken);
        Assert.Same(
            subscription.InitialSnapshot,
            initialMapper.Snapshot);
        Assert.Same(
            subscription.SnapshotSequence,
            initialMapper.Sequence);
        Assert.Equal(
            new[]
            {
                initialMessage,
                observationMapper.Messages[0],
                observationMapper.Messages[1]
            },
            stream.Messages);
        Assert.Equal(
            1,
            subscription.DisposeCount);
    }

    [Fact]
    public async Task Observe_ObservationServiceReturnsNull_ShouldThrow()
    {
        RuntimeHostRemoteApiService service =
            CreateConfiguredService(
                new TestObservationService(
                    null!),
                new TestInitialMapper(
                    new GrpcV1.ObserveResponse()),
                new TestObservationMapper());

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    service.Observe(
                        new GrpcV1.ObserveRequest(),
                        new TestStreamWriter(),
                        null!));

        Assert.Equal(
            "The runtime-host observation service returned null.",
            exception.Message);
    }

    [Fact]
    public async Task Observe_InitialMapperReturnsNull_ShouldDispose()
    {
        TestObservationSubscription subscription =
            CreateSubscription();
        RuntimeHostRemoteApiService service =
            CreateConfiguredService(
                new TestObservationService(
                    subscription),
                new TestInitialMapper(
                    null!),
                new TestObservationMapper());

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    service.Observe(
                        new GrpcV1.ObserveRequest(),
                        new TestStreamWriter(),
                        null!));

        Assert.Equal(
            "The initial observation snapshot mapper returned null.",
            exception.Message);
        Assert.Equal(
            1,
            subscription.DisposeCount);
    }

    [Fact]
    public async Task Observe_ObservationMapperReturnsNull_ShouldDispose()
    {
        TestObservationSubscription subscription =
            CreateSubscription(
                CreateObservation(
                    1));
        RuntimeHostRemoteApiService service =
            CreateConfiguredService(
                new TestObservationService(
                    subscription),
                new TestInitialMapper(
                    new GrpcV1.ObserveResponse()),
                new TestObservationMapper(
                    returnNull: true));

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    service.Observe(
                        new GrpcV1.ObserveRequest(),
                        new TestStreamWriter(),
                        null!));

        Assert.Equal(
            "The runtime-host observation mapper returned null.",
            exception.Message);
        Assert.Equal(
            1,
            subscription.DisposeCount);
    }

    [Fact]
    public async Task Observe_ObservationGap_ShouldTerminateWithDataLossAndDispose()
    {
        var subscription =
            new TestObservationSubscription(
                new Northbound.PublishedRuntimeHostSnapshot(
                    new Northbound.RuntimeHostId(
                        "runtime-host-1"),
                    Northbound.RuntimeHostApiVersion.Current,
                    Array.Empty<Northbound.PublishedRuntimeEndpointSnapshot>()),
                new Northbound.RuntimeHostObservationSequence(
                    0),
                Array.Empty<Northbound.RuntimeHostObservation>(),
                throwObservationGap:
                    true);
        RuntimeHostRemoteApiService service =
            CreateConfiguredService(
                new TestObservationService(
                    subscription),
                new TestInitialMapper(
                    new GrpcV1.ObserveResponse()),
                new TestObservationMapper());

        RpcException exception =
            await Assert.ThrowsAsync<RpcException>(
                () =>
                    service.Observe(
                        new GrpcV1.ObserveRequest(),
                        new TestStreamWriter(),
                        null!));

        Assert.Equal(
            StatusCode.DataLoss,
            exception.StatusCode);
        Assert.Equal(
            "The observation stream has a gap. Open a new subscription.",
            exception.Status.Detail);
        Assert.Equal(
            1,
            subscription.DisposeCount);
    }

    private static RuntimeHostRemoteApiService CreateConfiguredService(
        Northbound.IRuntimeHostObservationService observationService,
        IObservationInitialSnapshotMapper initialSnapshotMapper,
        IRuntimeHostObservationMapper observationMapper)
    {
        return CreateService(
            observationService,
            initialSnapshotMapper,
            observationMapper);
    }

    private static RuntimeHostRemoteApiService CreateService(
        Northbound.IRuntimeHostObservationService? observationService,
        IObservationInitialSnapshotMapper? initialSnapshotMapper,
        IRuntimeHostObservationMapper? observationMapper)
    {
        return new RuntimeHostRemoteApiService(
            new TestSnapshotProvider(),
            RuntimeHostSnapshotMapperFactory.Create(),
            observationService:
                observationService,
            initialSnapshotMapper:
                initialSnapshotMapper,
            observationMapper:
                observationMapper);
    }

    private static TestObservationSubscription CreateSubscription(
        params Northbound.RuntimeHostObservation[] observations)
    {
        return new TestObservationSubscription(
            new Northbound.PublishedRuntimeHostSnapshot(
                new Northbound.RuntimeHostId(
                    "runtime-host-1"),
                Northbound.RuntimeHostApiVersion.Current,
                Array.Empty<Northbound.PublishedRuntimeEndpointSnapshot>()),
            new Northbound.RuntimeHostObservationSequence(
                0),
            observations);
    }

    private static Northbound.RuntimeHostObservation CreateObservation(
        long sequence)
    {
        return new Northbound.RuntimeHostObservation(
            new Northbound.RuntimeHostObservationSequence(
                sequence),
            new EndpointId(
                "endpoint-01"),
            new Northbound.RuntimeEndpointAttachmentGeneration(
                new Guid(
                    "868e79d4-b1a4-4a63-81cd-5a800d9ba3fd")),
            new Northbound.RuntimeHostAttachmentEndedObservationPayload(
                DateTimeOffset.UnixEpoch));
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

    private sealed class TestObservationService
        : Northbound.IRuntimeHostObservationService
    {
        private readonly Northbound.RuntimeHostObservationSubscription
            subscription;

        public TestObservationService(
            Northbound.RuntimeHostObservationSubscription subscription)
        {
            this.subscription =
                subscription;
        }

        public int OpenCount
        {
            get;
            private set;
        }

        public Northbound.RuntimeHostObservationSubscriptionOptions? Options
        {
            get;
            private set;
        }

        public CancellationToken CancellationToken
        {
            get;
            private set;
        }

        public Task<Northbound.RuntimeHostObservationSubscription>
            OpenSubscriptionAsync(
                Northbound.RuntimeHostObservationSubscriptionOptions options,
                CancellationToken cancellationToken = default)
        {
            OpenCount++;
            Options =
                options;
            CancellationToken =
                cancellationToken;

            return Task.FromResult(
                subscription);
        }
    }

    private sealed class TestObservationSubscription
        : Northbound.RuntimeHostObservationSubscription
    {
        private readonly IReadOnlyList<Northbound.RuntimeHostObservation>
            observations;
        private readonly bool throwObservationGap;

        public TestObservationSubscription(
            Northbound.PublishedRuntimeHostSnapshot initialSnapshot,
            Northbound.RuntimeHostObservationSequence snapshotSequence,
            IReadOnlyList<Northbound.RuntimeHostObservation> observations,
            bool throwObservationGap = false)
            : base(
                initialSnapshot,
                snapshotSequence)
        {
            this.observations =
                observations;
            this.throwObservationGap =
                throwObservationGap;
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
            foreach (
                Northbound.RuntimeHostObservation observation
                in observations)
            {
                cancellationToken.ThrowIfCancellationRequested();

                yield return observation;
            }

            if (throwObservationGap)
            {
                throw new Northbound.RuntimeHostObservationGapException();
            }

            await Task.CompletedTask;
        }

        public override ValueTask DisposeAsync()
        {
            DisposeCount++;

            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestInitialMapper
        : IObservationInitialSnapshotMapper
    {
        private readonly GrpcV1.ObserveResponse result;

        public TestInitialMapper(
            GrpcV1.ObserveResponse result)
        {
            this.result =
                result;
        }

        public Northbound.PublishedRuntimeHostSnapshot? Snapshot
        {
            get;
            private set;
        }

        public Northbound.RuntimeHostObservationSequence? Sequence
        {
            get;
            private set;
        }

        public GrpcV1.ObserveResponse Map(
            Northbound.PublishedRuntimeHostSnapshot snapshot,
            Northbound.RuntimeHostObservationSequence snapshotSequence)
        {
            Snapshot =
                snapshot;
            Sequence =
                snapshotSequence;

            return result;
        }
    }

    private sealed class TestObservationMapper
        : IRuntimeHostObservationMapper
    {
        private readonly bool returnNull;

        public TestObservationMapper(
            bool returnNull = false)
        {
            this.returnNull =
                returnNull;
        }

        public List<GrpcV1.ObserveResponse> Messages
        {
            get;
        } =
            [];

        public GrpcV1.ObserveResponse Map(
            Northbound.RuntimeHostObservation observation)
        {
            if (returnNull)
            {
                return null!;
            }

            var message =
                new GrpcV1.ObserveResponse
                {
                    Observation =
                        new GrpcV1.RuntimeHostObservation
                        {
                            Sequence =
                                checked(
                                    (ulong)observation.Sequence.Value)
                        }
                };

            Messages.Add(
                message);

            return message;
        }
    }

    private sealed class TestStreamWriter
        : IServerStreamWriter<GrpcV1.ObserveResponse>
    {
        public WriteOptions? WriteOptions
        {
            get;
            set;
        }

        public List<GrpcV1.ObserveResponse> Messages
        {
            get;
        } =
            [];

        public Task WriteAsync(
            GrpcV1.ObserveResponse message)
        {
            Messages.Add(
                message);

            return Task.CompletedTask;
        }
    }
}
