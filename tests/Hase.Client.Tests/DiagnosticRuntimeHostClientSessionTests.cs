using Hase.Client.Diagnostics;

namespace Hase.Client.Tests;

public sealed class DiagnosticRuntimeHostClientSessionTests
{
    [Fact]
    public async Task ReadStatesAsync_RecordsLifecycleAndObservationWithoutChangingStates()
    {
        var inner = new StubSession([RemoteObservationState.Empty]);
        BoundedClientDiagnosticCollector collector = new(20);
        await using var session = new DiagnosticRuntimeHostClientSession(
            inner,
            new ClientDiagnosticPublisher(collector));

        List<RemoteObservationState> states = [];
        await foreach (RemoteObservationState state in session.ReadStatesAsync())
        {
            states.Add(state);
        }

        Assert.Same(RemoteObservationState.Empty, Assert.Single(states));
        Assert.Equal(
            new[]
            {
                "SessionStarted",
                "ObservationSubscriptionStarted",
                "SnapshotDelivered",
                "ObservationSubscriptionEnded"
            },
            collector.GetSnapshot().Records.Select(record => record.EventName));
        Assert.Equal(
            ClientDiagnosticOutcome.Succeeded,
            collector.GetSnapshot().Records[^1].Outcome);
    }

    [Fact]
    public async Task ReadStatesAsync_Failure_RecordsFailureAndPreservesException()
    {
        var expected = new IOException("Stream failed.");
        var inner = new StubSession([], expected);
        BoundedClientDiagnosticCollector collector = new(20);
        await using var session = new DiagnosticRuntimeHostClientSession(
            inner,
            new ClientDiagnosticPublisher(collector));

        IOException actual = await Assert.ThrowsAsync<IOException>(
            async () => await ReadAllAsync(session.ReadStatesAsync()));

        Assert.Same(expected, actual);
        ClientDiagnosticRecord ended = collector.GetSnapshot().Records[^1];
        Assert.Equal("ObservationSubscriptionEnded", ended.EventName);
        Assert.Equal(ClientDiagnosticOutcome.Failed, ended.Outcome);
    }

    [Fact]
    public async Task StatusTransition_IsForwardedAndRecordedWithoutHostIdentity()
    {
        var inner = new StubSession([]);
        BoundedClientDiagnosticCollector collector = new(20);
        await using var session = new DiagnosticRuntimeHostClientSession(
            inner,
            new ClientDiagnosticPublisher(collector));
        RuntimeHostClientSessionStatusChangedEventArgs? forwarded = null;
        session.StatusChanged += (_, args) => forwarded = args;

        inner.SetStatus(RuntimeHostClientSessionState.Connecting);

        Assert.NotNull(forwarded);
        ClientDiagnosticRecord record = Assert.Single(collector.GetSnapshot().Records);
        Assert.Equal("ConnectionStarted", record.EventName);
        Assert.Null(record.EndpointId);
        Assert.Equal("Connecting", record.Metadata["CurrentState"]);
    }

    [Fact]
    public async Task DisposeAsync_RecordsSessionStoppedAndDisposesInnerOnce()
    {
        var inner = new StubSession([]);
        BoundedClientDiagnosticCollector collector = new(20);
        var session = new DiagnosticRuntimeHostClientSession(
            inner,
            new ClientDiagnosticPublisher(collector));

        await session.DisposeAsync();
        await session.DisposeAsync();

        Assert.Equal(1, inner.DisposeCount);
        Assert.Equal("SessionStopped", Assert.Single(collector.GetSnapshot().Records).EventName);
    }

    private static async Task ReadAllAsync(IAsyncEnumerable<RemoteObservationState> states)
    {
        await foreach (RemoteObservationState _ in states)
        {
        }
    }

    private sealed class StubSession : IRuntimeHostClientSession
    {
        private readonly IReadOnlyList<RemoteObservationState> states;
        private readonly Exception? failure;

        public StubSession(
            IReadOnlyList<RemoteObservationState> states,
            Exception? failure = null)
        {
            this.states = states;
            this.failure = failure;
        }

        public event EventHandler<RuntimeHostClientSessionStatusChangedEventArgs>? StatusChanged;
        public RuntimeHostClientSessionStatus Status { get; private set; } =
            new(RuntimeHostClientSessionState.Disconnected);
        public RemoteObservationState? CurrentState => null;
        public int DisposeCount { get; private set; }

        public async IAsyncEnumerable<RemoteObservationState> ReadStatesAsync(
            CancellationToken cancellationToken = default)
        {
            foreach (RemoteObservationState state in states)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return state;
            }

            if (failure is not null)
            {
                throw failure;
            }

            await Task.CompletedTask;
        }

        public void SetStatus(RuntimeHostClientSessionState state)
        {
            RuntimeHostClientSessionStatus previous = Status;
            Status = new RuntimeHostClientSessionStatus(state);
            StatusChanged?.Invoke(
                this,
                new RuntimeHostClientSessionStatusChangedEventArgs(previous, Status));
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
