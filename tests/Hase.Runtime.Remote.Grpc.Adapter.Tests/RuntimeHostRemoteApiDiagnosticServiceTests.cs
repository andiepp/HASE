using System.Runtime.CompilerServices;
using Grpc.Core;
using Microsoft.Extensions.Hosting;
using RuntimeDiagnostics = global::Hase.Runtime.Diagnostics;
using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostRemoteApiDiagnosticServiceTests
{
    [Fact]
    public void Constructor_IncompleteDiagnosticDependencies_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "diagnosticProjectionService",
            () => CreateApi(null, new RuntimeHostProjectedDiagnosticObservationMapper()));
        Assert.Throws<ArgumentNullException>(
            "diagnosticObservationMapper",
            () => CreateApi(CreateProjectionService(), null));
    }

    [Fact]
    public async Task ObserveDiagnostics_NullRequest_ShouldThrow()
    {
        await using Northbound.RuntimeHostDiagnosticProjectionService projection =
            CreateProjectionService();
        RuntimeHostRemoteApiService api = CreateApi(
            projection,
            new RuntimeHostProjectedDiagnosticObservationMapper());

        await Assert.ThrowsAsync<ArgumentNullException>(
            "request",
            () => api.ObserveDiagnostics(null!, new TestStreamWriter(), null!));
    }

    [Fact]
    public async Task ObserveDiagnostics_NullStream_ShouldThrow()
    {
        await using Northbound.RuntimeHostDiagnosticProjectionService projection =
            CreateProjectionService();
        RuntimeHostRemoteApiService api = CreateApi(
            projection,
            new RuntimeHostProjectedDiagnosticObservationMapper());

        await Assert.ThrowsAsync<ArgumentNullException>(
            "responseStream",
            () => api.ObserveDiagnostics(
                new GrpcV1.ObserveDiagnosticsRequest(),
                null!,
                null!));
    }

    [Fact]
    public async Task ObserveDiagnostics_NotConfigured_ShouldThrow()
    {
        RuntimeHostRemoteApiService api = CreateApi();

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                api.ObserveDiagnostics(
                    new GrpcV1.ObserveDiagnosticsRequest(),
                    new TestStreamWriter(),
                    null!));

        Assert.Equal(
            "Runtime-host diagnostic projection is not configured.",
            exception.Message);
    }

    [Fact]
    public async Task ObserveDiagnostics_StreamsLiveRecordsInOrder()
    {
        await using Northbound.RuntimeHostDiagnosticProjectionService projection =
            CreateProjectionService();
        using var cancellation = new CancellationTokenSource();
        var context = new TestServerCallContext(cancellation.Token);
        var stream = new TestStreamWriter(expectedCount: 2, cancellation);
        RuntimeHostRemoteApiService api = CreateApi(
            projection,
            new RuntimeHostProjectedDiagnosticObservationMapper());
        Task observing = api.ObserveDiagnostics(
            new GrpcV1.ObserveDiagnosticsRequest(),
            stream,
            context);
        var publisher = new RuntimeDiagnostics.RuntimeDiagnosticPublisher(projection);

        Publish(publisher, "First");
        Publish(publisher, "Second");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => observing);
        Assert.Equal(2, stream.Messages.Count);
        Assert.Equal((ulong)1, stream.Messages[0].Sequence);
        Assert.Equal((ulong)2, stream.Messages[1].Sequence);
        Assert.Equal("First", stream.Messages[0].Record.EventName);
        Assert.Equal("Second", stream.Messages[1].Record.EventName);
    }

    [Fact]
    public async Task ObserveDiagnostics_RequestCancellation_IsPreserved()
    {
        await using Northbound.RuntimeHostDiagnosticProjectionService projection =
            CreateProjectionService();
        using var cancellation = new CancellationTokenSource();
        RuntimeHostRemoteApiService api = CreateApi(
            projection,
            new RuntimeHostProjectedDiagnosticObservationMapper());
        Task observing = api.ObserveDiagnostics(
            new GrpcV1.ObserveDiagnosticsRequest(),
            new TestStreamWriter(),
            new TestServerCallContext(cancellation.Token));

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => observing);
    }

    [Fact]
    public async Task ObserveDiagnostics_HostShutdownCancellation_IsPreserved()
    {
        await using Northbound.RuntimeHostDiagnosticProjectionService projection =
            CreateProjectionService();
        var lifetime = new TestApplicationLifetime();
        RuntimeHostRemoteApiService api = CreateApi(
            projection,
            new RuntimeHostProjectedDiagnosticObservationMapper(),
            lifetime: lifetime);
        Task observing = api.ObserveDiagnostics(
            new GrpcV1.ObserveDiagnosticsRequest(),
            new TestStreamWriter(),
            null!);

        lifetime.StopApplication();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => observing);
    }

    [Fact]
    public async Task ObserveDiagnostics_SlowSubscriberGap_ShouldReturnDataLoss()
    {
        await using Northbound.RuntimeHostDiagnosticProjectionService projection =
            CreateProjectionService();
        var stream = new BlockingFirstWriteStream();
        RuntimeHostRemoteApiService api = CreateApi(
            projection,
            new RuntimeHostProjectedDiagnosticObservationMapper());
        Task observing = api.ObserveDiagnostics(
            new GrpcV1.ObserveDiagnosticsRequest(),
            stream,
            null!);
        var publisher = new RuntimeDiagnostics.RuntimeDiagnosticPublisher(projection);
        Publish(publisher, "First");
        await stream.FirstWriteStarted.Task;

        for (int index = 0; index < 257; index++)
        {
            Publish(publisher, $"Buffered{index}");
        }

        stream.ReleaseFirstWrite.SetResult();
        RpcException exception = await Assert.ThrowsAsync<RpcException>(() => observing);

        Assert.Equal(StatusCode.DataLoss, exception.StatusCode);
        Assert.Equal(
            "The diagnostic stream has a gap. Open a new subscription.",
            exception.Status.Detail);
    }

    [Fact]
    public async Task ObserveDiagnostics_Denied_ShouldAuthorizeBeforeSubscription()
    {
        await using Northbound.RuntimeHostDiagnosticProjectionService projection =
            CreateProjectionService();
        var gate = new TestAuthorizationGate(
            RuntimeHostAuthorizationDecision.Deny("Not granted."));
        RuntimeHostRemoteApiService api = CreateApi(
            projection,
            new RuntimeHostProjectedDiagnosticObservationMapper(),
            principalProvider: new TestPrincipalProvider(),
            authorizationGate: gate);

        RpcException exception = await Assert.ThrowsAsync<RpcException>(() =>
            api.ObserveDiagnostics(
                new GrpcV1.ObserveDiagnosticsRequest(),
                new TestStreamWriter(),
                null!));

        Assert.Equal(StatusCode.PermissionDenied, exception.StatusCode);
        Assert.Equal(
            RuntimeHostRemoteOperation.ObserveDiagnostics,
            gate.ObservedOperation);
    }

    [Fact]
    public async Task ObserveDiagnostics_StreamFailure_IsPreserved()
    {
        await using Northbound.RuntimeHostDiagnosticProjectionService projection =
            CreateProjectionService();
        RuntimeHostRemoteApiService api = CreateApi(
            projection,
            new RuntimeHostProjectedDiagnosticObservationMapper());
        Task observing = api.ObserveDiagnostics(
            new GrpcV1.ObserveDiagnosticsRequest(),
            new ThrowingStreamWriter(),
            null!);
        Publish(new RuntimeDiagnostics.RuntimeDiagnosticPublisher(projection), "Record");

        await Assert.ThrowsAsync<InvalidOperationException>(() => observing);
    }

    private static RuntimeHostRemoteApiService CreateApi(
        Northbound.RuntimeHostDiagnosticProjectionService? projection = null,
        RuntimeHostProjectedDiagnosticObservationMapper? mapper = null,
        IHostApplicationLifetime? lifetime = null,
        IRuntimeHostClientPrincipalProvider? principalProvider = null,
        IRuntimeHostRemoteAuthorizationGate? authorizationGate = null)
    {
        return new RuntimeHostRemoteApiService(
            new TestSnapshotProvider(),
            RuntimeHostSnapshotMapperFactory.Create(),
            applicationLifetime: lifetime,
            principalProvider: principalProvider,
            authorizationGate: authorizationGate,
            diagnosticProjectionService: projection,
            diagnosticObservationMapper: mapper);
    }

    private static Northbound.RuntimeHostDiagnosticProjectionService
        CreateProjectionService()
    {
        return new Northbound.RuntimeHostDiagnosticProjectionService(
            new Northbound.RuntimeHostId("host-one"),
            new RuntimeDiagnostics.BoundedRuntimeDiagnosticCollector(1024),
            RuntimeDiagnostics.RuntimeDiagnosticLevel.Bytes,
            new Northbound.RuntimeHostDiagnosticProjectionPolicy(isEnabled: true));
    }

    private static void Publish(
        RuntimeDiagnostics.RuntimeDiagnosticPublisher publisher,
        string eventName)
    {
        publisher.Publish(new RuntimeDiagnostics.RuntimeDiagnosticEvent(
            RuntimeDiagnostics.RuntimeDiagnosticLevel.Operational,
            RuntimeDiagnostics.RuntimeDiagnosticCategory.RuntimeConnection,
            eventName));
    }

    private sealed class TestSnapshotProvider
        : Northbound.IRuntimeHostSnapshotProvider
    {
        public Northbound.PublishedRuntimeHostSnapshot Capture() =>
            new(
                new Northbound.RuntimeHostId("host-one"),
                Northbound.RuntimeHostApiVersion.Current,
                []);
    }

    private sealed class TestStreamWriter
        : IServerStreamWriter<GrpcV1.ProjectedDiagnosticObservation>
    {
        private readonly int expectedCount;
        private readonly CancellationTokenSource? cancellation;

        public TestStreamWriter(
            int expectedCount = int.MaxValue,
            CancellationTokenSource? cancellation = null)
        {
            this.expectedCount = expectedCount;
            this.cancellation = cancellation;
        }

        public WriteOptions? WriteOptions { get; set; }

        public List<GrpcV1.ProjectedDiagnosticObservation> Messages { get; } = [];

        public Task WriteAsync(GrpcV1.ProjectedDiagnosticObservation message)
        {
            Messages.Add(message);
            if (Messages.Count == expectedCount)
            {
                cancellation!.Cancel();
            }

            return Task.CompletedTask;
        }
    }

    private sealed class BlockingFirstWriteStream
        : IServerStreamWriter<GrpcV1.ProjectedDiagnosticObservation>
    {
        private int count;
        public TaskCompletionSource FirstWriteStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFirstWrite { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public WriteOptions? WriteOptions { get; set; }

        public async Task WriteAsync(GrpcV1.ProjectedDiagnosticObservation message)
        {
            if (Interlocked.Increment(ref count) == 1)
            {
                FirstWriteStarted.SetResult();
                await ReleaseFirstWrite.Task;
            }
        }
    }

    private sealed class ThrowingStreamWriter
        : IServerStreamWriter<GrpcV1.ProjectedDiagnosticObservation>
    {
        public WriteOptions? WriteOptions { get; set; }

        public Task WriteAsync(GrpcV1.ProjectedDiagnosticObservation message) =>
            throw new InvalidOperationException("Expected stream failure.");
    }

    private sealed class TestApplicationLifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource stopping = new();
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => stopping.Token;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() => stopping.Cancel();
    }

    private sealed class TestPrincipalProvider
        : IRuntimeHostClientPrincipalProvider
    {
        public RuntimeHostClientPrincipal GetPrincipal(ServerCallContext? context) =>
            new(
                "principal",
                "credential",
                "test",
                DateTimeOffset.UnixEpoch,
                "test-policy");
    }

    private sealed class TestAuthorizationGate
        : IRuntimeHostRemoteAuthorizationGate
    {
        private readonly RuntimeHostAuthorizationDecision decision;
        public TestAuthorizationGate(RuntimeHostAuthorizationDecision decision) =>
            this.decision = decision;
        public RuntimeHostRemoteOperation? ObservedOperation { get; private set; }
        public RuntimeHostAuthorizationDecision Authorize(
            RuntimeHostClientPrincipal principal,
            RuntimeHostRemoteOperation operation)
        {
            ObservedOperation = operation;
            return decision;
        }
    }

    private sealed class TestServerCallContext : ServerCallContext
    {
        private readonly CancellationToken cancellationToken;
        public TestServerCallContext(CancellationToken cancellationToken) =>
            this.cancellationToken = cancellationToken;
        protected override string MethodCore => "ObserveDiagnostics";
        protected override string HostCore => "localhost";
        protected override string PeerCore => "test";
        protected override DateTime DeadlineCore => DateTime.MaxValue;
        protected override Metadata RequestHeadersCore => [];
        protected override CancellationToken CancellationTokenCore => cancellationToken;
        protected override Metadata ResponseTrailersCore { get; } = [];
        protected override Status StatusCore { get; set; }
        protected override WriteOptions? WriteOptionsCore { get; set; }
        protected override AuthContext AuthContextCore => null!;
        protected override ContextPropagationToken CreatePropagationTokenCore(
            ContextPropagationOptions? options) => throw new NotSupportedException();
        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) =>
            Task.CompletedTask;
    }
}
