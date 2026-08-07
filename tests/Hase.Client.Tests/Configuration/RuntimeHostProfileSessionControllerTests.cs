using System.Threading.Channels;
using Hase.Client.Configuration;
using Hase.Client.Diagnostics;

namespace Hase.Client.Tests.Configuration;

public sealed class RuntimeHostProfileSessionControllerTests
{
    [Fact]
    public void Constructor_ShouldExposeDisconnectedProfileSnapshot()
    {
        RuntimeHostProfile profile = CreateProfile("first", "host-01");
        var controller = new RuntimeHostProfileSessionController(profile, new FakeFactory(new FakeSession()));

        Assert.Same(profile, controller.Snapshot.Profile);
        Assert.Equal(RuntimeHostClientSessionState.Disconnected, controller.Snapshot.Status.State);
    }

    [Fact]
    public async Task ConnectAsync_ShouldResolveExactProfileAndRejectDuplicateConnection()
    {
        var factory = new FakeFactory(new FakeSession());
        await using var controller = new RuntimeHostProfileSessionController(CreateProfile("first", "host-01"), factory);

        await controller.ConnectAsync();

        Assert.Equal(new RuntimeHostProfileId("first"), factory.RequestedProfileId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.ConnectAsync());
    }

    [Fact]
    public async Task MatchingInitialState_ShouldPublishConnectedSnapshot()
    {
        var session = new FakeSession();
        await using var controller = new RuntimeHostProfileSessionController(CreateProfile("first", "host-01"), new FakeFactory(session));
        await controller.ConnectAsync();

        session.Publish(CreateState("host-01"));
        await WaitUntilAsync(() => controller.Snapshot.Status.State == RuntimeHostClientSessionState.Connected);

        Assert.True(controller.Snapshot.CurrentState!.IsInitialized);
        Assert.Null(controller.Snapshot.Failure);
    }

    [Fact]
    public async Task MismatchedInitialState_ShouldFailClosed()
    {
        var session = new FakeSession();
        await using var controller = new RuntimeHostProfileSessionController(CreateProfile("first", "host-01"), new FakeFactory(session));
        await controller.ConnectAsync();

        session.Publish(CreateState("host-02"));
        await WaitUntilAsync(() => controller.Snapshot.Status.State == RuntimeHostClientSessionState.Faulted);

        Assert.Equal(RuntimeHostClientFailureCategory.InvalidRemoteContract, controller.Snapshot.Failure!.Category);
        Assert.Null(controller.Snapshot.CurrentState);
    }

    [Fact]
    public async Task DisconnectAsync_ShouldCancelDisposeAndBecomeIdempotent()
    {
        var session = new FakeSession();
        await using var controller = new RuntimeHostProfileSessionController(CreateProfile("first", "host-01"), new FakeFactory(session));
        await controller.ConnectAsync();

        await controller.DisconnectAsync();
        await controller.DisconnectAsync();

        Assert.True(session.WasCancelled);
        Assert.Equal(1, session.DisposeCount);
        Assert.Equal(RuntimeHostClientSessionState.Disconnected, controller.Snapshot.Status.State);
    }

    [Fact]
    public async Task DisposeAsync_ShouldBeIdempotentAndRejectLaterConnect()
    {
        var controller = new RuntimeHostProfileSessionController(CreateProfile("first", "host-01"), new FakeFactory(new FakeSession()));
        await controller.DisposeAsync();
        await controller.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => controller.ConnectAsync());
    }

    [Fact]
    public async Task SeparateControllers_ShouldRemainIndependent()
    {
        var firstSession = new FakeSession();
        var secondSession = new FakeSession();
        await using var first = new RuntimeHostProfileSessionController(CreateProfile("first", "host-01"), new FakeFactory(firstSession));
        await using var second = new RuntimeHostProfileSessionController(CreateProfile("second", "host-02"), new FakeFactory(secondSession));
        await first.ConnectAsync();
        await second.ConnectAsync();

        firstSession.Publish(CreateState("wrong-host"));
        secondSession.Publish(CreateState("host-02"));
        await WaitUntilAsync(() => first.Snapshot.Status.State == RuntimeHostClientSessionState.Faulted);
        await WaitUntilAsync(() => second.Snapshot.Status.State == RuntimeHostClientSessionState.Connected);

        Assert.Equal(RuntimeHostClientSessionState.Connected, second.Snapshot.Status.State);
        Assert.False(secondSession.WasCancelled);
    }

    [Fact]
    public async Task StateTransitionDiagnostic_ShouldCarryExactProfileContext()
    {
        BoundedClientDiagnosticCollector collector = new(10);
        var diagnostics = new ClientDiagnosticPublisher(collector);
        await using var controller = new RuntimeHostProfileSessionController(
            CreateProfile("first", "host-01"),
            new FakeFactory(new FakeSession()),
            diagnostics);

        await controller.ConnectAsync();

        ClientDiagnosticRecord record = Assert.Single(collector.GetSnapshot().Records);
        Assert.Equal("RuntimeHostProfileSessionStateChanged", record.EventName);
        Assert.Equal("first", record.RuntimeHostProfileId);
        Assert.Equal("first", record.RuntimeHostProfileDisplayName);
        Assert.Equal("host-01", record.ExpectedRuntimeHostId);
        Assert.Null(record.AuthoritativeRuntimeHostId);
    }

    [Fact]
    public async Task RemoteDiagnostic_ShouldPublishWithHostTimestampAndProfileContext()
    {
        BoundedClientDiagnosticCollector collector =
            new(10, ClientDiagnosticLevel.Bytes);
        var diagnostics = new ClientDiagnosticPublisher(collector);
        var session = new FakeSession();
        await using var controller = new RuntimeHostProfileSessionController(
            CreateProfile("first", "host-01"),
            new FakeFactory(session),
            diagnostics);
        await controller.ConnectAsync();
        session.Publish(CreateState("host-01"));
        await WaitUntilAsync(() =>
            controller.Snapshot.Status.State == RuntimeHostClientSessionState.Connected);
        DateTimeOffset timestamp = new(
            2026, 8, 7, 10, 20, 30, TimeSpan.Zero);

        session.PublishDiagnostic(
            new RemoteRuntimeDiagnosticObservation(
                1,
                new RemoteRuntimeDiagnosticRecord(
                    "host-01",
                    2,
                    timestamp,
                    RemoteRuntimeDiagnosticLevel.Protocol,
                    RemoteRuntimeDiagnosticCategory.ProtocolExchange,
                    "ScpiQuery",
                    RemoteRuntimeDiagnosticSeverity.Information)));

        ClientDiagnosticRecord record = collector.GetSnapshot().Records.Last();
        Assert.Equal("ScpiQuery", record.EventName);
        Assert.Equal(timestamp, record.TimestampUtc);
        Assert.Equal("first", record.RuntimeHostProfileId);
        Assert.Equal("host-01", record.AuthoritativeRuntimeHostId);
    }

    [Fact]
    public async Task DiagnosticStreamFailure_ShouldPublishSanitizedStateOnly()
    {
        BoundedClientDiagnosticCollector collector = new(10);
        var session = new FakeSession();
        await using var controller = new RuntimeHostProfileSessionController(
            CreateProfile("first", "host-01"),
            new FakeFactory(session),
            new ClientDiagnosticPublisher(collector));
        await controller.ConnectAsync();

        session.FailDiagnostics(
            RemoteRuntimeDiagnosticStreamFailureKind.AuthorizationDenied,
            new IOException("sensitive transport detail"));

        ClientDiagnosticRecord record = collector.GetSnapshot().Records.Last();
        Assert.Equal("RemoteDiagnosticAuthorizationDenied", record.EventName);
        Assert.DoesNotContain(
            "sensitive",
            string.Join(" ", record.Metadata.Values),
            StringComparison.OrdinalIgnoreCase);
    }

    private static RuntimeHostProfile CreateProfile(string id, string host) =>
        new(new RuntimeHostProfileId(id), id, new RemoteRuntimeHostId(host));

    private static RemoteObservationState CreateState(string host) =>
        new RemoteObservationReducer().Initialize(
            RemoteObservationState.Empty,
            new RemoteObservationInitialSnapshot(
                new RemoteRuntimeHostSnapshot(new RemoteRuntimeHostId(host), RuntimeHostClientApiVersion.Current, []),
                new RemoteObservationSequence(0)));

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        for (int index = 0; index < 100 && !predicate(); index++) await Task.Delay(10);
        Assert.True(predicate());
    }

    private sealed class FakeFactory(FakeSession session) : IRuntimeHostProfileClientSessionFactory
    {
        public RuntimeHostProfileId? RequestedProfileId { get; private set; }
        public Task<IRuntimeHostClientSession> CreateAsync(RuntimeHostProfileId profileId, CancellationToken cancellationToken = default)
        { RequestedProfileId = profileId; return Task.FromResult<IRuntimeHostClientSession>(session); }
    }

    private sealed class FakeSession
        : IRuntimeHostClientSession,
          IRuntimeHostDiagnosticSource
    {
        private readonly Channel<RemoteObservationState> states = Channel.CreateUnbounded<RemoteObservationState>();
        public event EventHandler<RuntimeHostClientSessionStatusChangedEventArgs>? StatusChanged;
        public event EventHandler<RemoteRuntimeDiagnosticObservedEventArgs>? DiagnosticObserved;
        public event EventHandler<RemoteRuntimeDiagnosticStreamFaultedEventArgs>? DiagnosticStreamFaulted;
        public RuntimeHostClientSessionStatus Status { get; private set; } = new(RuntimeHostClientSessionState.Disconnected);
        public RemoteObservationState? CurrentState { get; private set; }
        public bool WasCancelled { get; private set; }
        public int DisposeCount { get; private set; }

        public void Publish(RemoteObservationState state)
        {
            RuntimeHostClientSessionStatus previous = Status;
            Status = new(RuntimeHostClientSessionState.Connected, state.Snapshot!.RuntimeHostId, state.Snapshot.ApiVersion);
            StatusChanged?.Invoke(this, new(previous, Status));
            states.Writer.TryWrite(state);
        }

        public void PublishDiagnostic(RemoteRuntimeDiagnosticObservation observation) =>
            DiagnosticObserved?.Invoke(
                this,
                new RemoteRuntimeDiagnosticObservedEventArgs(observation));

        public void FailDiagnostics(
            RemoteRuntimeDiagnosticStreamFailureKind kind,
            Exception exception) =>
            DiagnosticStreamFaulted?.Invoke(
                this,
                new RemoteRuntimeDiagnosticStreamFaultedEventArgs(
                    kind,
                    exception));

        public async IAsyncEnumerable<RemoteObservationState> ReadStatesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            try
            {
                await foreach (RemoteObservationState state in states.Reader.ReadAllAsync(cancellationToken))
                { CurrentState = state; yield return state; }
            }
            finally { WasCancelled = cancellationToken.IsCancellationRequested; }
        }

        public ValueTask DisposeAsync() { DisposeCount++; states.Writer.TryComplete(); return ValueTask.CompletedTask; }
    }
}
