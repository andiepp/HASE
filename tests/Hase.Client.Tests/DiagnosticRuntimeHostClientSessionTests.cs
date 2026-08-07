using Hase.Client.Diagnostics;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Client.Tests;

public sealed class DiagnosticRuntimeHostClientSessionTests
{
    [Fact]
    public async Task ReadStatesAsync_ProtocolCapture_CorrelatesObserveRequestAndResponse()
    {
        var inner = new StubSession([RemoteObservationState.Empty]);
        BoundedClientDiagnosticCollector collector =
            new(20, ClientDiagnosticLevel.Protocol);
        await using var session = new DiagnosticRuntimeHostClientSession(
            inner,
            new ClientDiagnosticPublisher(collector));

        await ReadAllAsync(session.ReadStatesAsync());

        ClientDiagnosticRecord[] protocol = collector.GetSnapshot(
            ClientDiagnosticLevel.Protocol).Records.ToArray();
        Assert.Contains(protocol, record => record.EventName == "ObserveRequest");
        Assert.Contains(protocol, record => record.EventName == "InitialSnapshotResponse");
        Assert.Single(protocol.Select(record => record.OperationId).Distinct());
        Assert.All(protocol, record => Assert.Equal("Observe", record.Metadata["ApiOperation"]));
    }

    [Fact]
    public async Task PropertyRead_ProtocolCapture_ContainsTargetAndStatusButNoValue()
    {
        var inner = new StubSession([]);
        BoundedClientDiagnosticCollector collector =
            new(20, ClientDiagnosticLevel.Protocol);
        await using var session = new DiagnosticRuntimeHostClientSession(
            inner,
            new ClientDiagnosticPublisher(collector));
        RemotePropertyTarget target = CreatePropertyTarget();

        RemotePropertyOperationResult result = await session.ReadPropertyAsync(target);

        Assert.False(result.IsSuccess);
        ClientDiagnosticRecord response = collector.GetSnapshot().Records.Single(
            record => record.EventName == "PropertyReadResponse");
        Assert.Equal("endpoint-01", response.EndpointId);
        Assert.Equal("property-01", response.DescriptorPath);
        Assert.Equal("EndpointUnavailable", response.Metadata["ResultStatus"]);
        Assert.DoesNotContain(response.Metadata.Keys, key => key.Contains("Value", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CommandExecution_ProtocolCapture_ContainsTargetButNoArgumentOrReturnValue()
    {
        var inner = new StubSession([]);
        BoundedClientDiagnosticCollector collector =
            new(20, ClientDiagnosticLevel.Protocol);
        await using var session = new DiagnosticRuntimeHostClientSession(
            inner,
            new ClientDiagnosticPublisher(collector));
        var request = new RemoteCommandExecutionRequest(
            new RemoteCommandTarget(
                CreateAttachment(),
                new InstrumentId("instrument-01"),
                DescriptorPath.Parse("Led.Toggle")),
            RemoteValue.FromString("must-not-be-captured"));

        await session.ExecuteCommandAsync(request);

        ClientDiagnosticRecord response = collector.GetSnapshot().Records.Single(
            record => record.EventName == "CommandExecutionResponse");
        Assert.Equal("Led.Toggle", response.DescriptorPath);
        Assert.DoesNotContain(
            response.Metadata.Values,
            value => value.Contains("must-not-be-captured", StringComparison.Ordinal));
    }

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

    [Fact]
    public async Task DiagnosticSource_ObservationAndFault_ShouldForwardUnchanged()
    {
        var inner = new DiagnosticStubSession([]);
        await using var session = new DiagnosticRuntimeHostClientSession(
            inner,
            new ClientDiagnosticPublisher());
        IRuntimeHostDiagnosticSource source = session;
        RemoteRuntimeDiagnosticObservedEventArgs? observed = null;
        RemoteRuntimeDiagnosticStreamFaultedEventArgs? faulted = null;
        source.DiagnosticObserved += (_, args) => observed = args;
        source.DiagnosticStreamFaulted += (_, args) => faulted = args;
        var observation = new RemoteRuntimeDiagnosticObservation(
            1,
            new RemoteRuntimeDiagnosticRecord(
                "runtime-01",
                1,
                DateTimeOffset.UnixEpoch,
                RemoteRuntimeDiagnosticLevel.Bytes,
                RemoteRuntimeDiagnosticCategory.TransportBytes,
                "TransportBytesReceived",
                RemoteRuntimeDiagnosticSeverity.Information,
                byteSnapshot: new RemoteRuntimeDiagnosticByteSnapshot(
                    2,
                    [0x31, 0x0A],
                    false)));
        var failure = new IOException("Diagnostic stream ended.");

        RemoteRuntimeDiagnosticObservedEventArgs expectedObserved =
            inner.PublishDiagnostic(observation);
        RemoteRuntimeDiagnosticStreamFaultedEventArgs expectedFaulted =
            inner.FailDiagnostics(
                RemoteRuntimeDiagnosticStreamFailureKind.TransportUnavailable,
                failure);

        Assert.Same(expectedObserved, observed);
        Assert.Same(expectedFaulted, faulted);
        Assert.Same(observation, observed!.Observation);
        Assert.Same(failure, faulted!.Exception);
    }

    [Fact]
    public async Task DisposeAsync_DiagnosticSource_ShouldUnsubscribeBeforeInnerDisposal()
    {
        var inner = new DiagnosticStubSession([]);
        var session = new DiagnosticRuntimeHostClientSession(
            inner,
            new ClientDiagnosticPublisher());
        IRuntimeHostDiagnosticSource source = session;
        int observedCount = 0;
        int faultedCount = 0;
        source.DiagnosticObserved += (_, _) => observedCount++;
        source.DiagnosticStreamFaulted += (_, _) => faultedCount++;

        await session.DisposeAsync();
        inner.PublishDiagnostic(CreateDiagnosticObservation());
        inner.FailDiagnostics(
            RemoteRuntimeDiagnosticStreamFailureKind.Unknown,
            new IOException("Late failure."));

        Assert.Equal(0, observedCount);
        Assert.Equal(0, faultedCount);
        Assert.Equal(1, inner.DisposeCount);
    }

    private static async Task ReadAllAsync(IAsyncEnumerable<RemoteObservationState> states)
    {
        await foreach (RemoteObservationState _ in states)
        {
        }
    }

    private static RemotePropertyTarget CreatePropertyTarget() =>
        new(
            CreateAttachment(),
            new InstrumentId("instrument-01"),
            new PropertyId("property-01"));

    private static RemoteEndpointAttachmentKey CreateAttachment() =>
        new(
            new EndpointId("endpoint-01"),
            new RemoteEndpointAttachmentGeneration(
                Guid.Parse("0a11d9d4-7a02-43be-ae3f-eef9d11e0de8")));

    private static RemoteRuntimeDiagnosticObservation
        CreateDiagnosticObservation() =>
        new(
            1,
            new RemoteRuntimeDiagnosticRecord(
                "runtime-01",
                1,
                DateTimeOffset.UnixEpoch,
                RemoteRuntimeDiagnosticLevel.Operational,
                RemoteRuntimeDiagnosticCategory.RuntimeConnection,
                "Connected",
                RemoteRuntimeDiagnosticSeverity.Information));

    private class StubSession
        : IRuntimeHostClientSession,
          IRuntimeHostPropertyReader,
          IRuntimeHostPropertyWriter,
          IRuntimeHostCommandExecutor
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

        public Task<RemotePropertyOperationResult> ReadPropertyAsync(
            RemotePropertyTarget target,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                RemotePropertyOperationResult.Failed(
                    RemotePropertyOperationStatus.EndpointUnavailable));

        public Task<RemotePropertyOperationResult> WritePropertyAsync(
            RemotePropertyTarget target,
            RemoteValue requestedValue,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                RemotePropertyOperationResult.Failed(
                    RemotePropertyOperationStatus.InvalidValue));

        public Task<RemoteCommandOperationResult> ExecuteCommandAsync(
            RemoteCommandExecutionRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(RemoteCommandOperationResult.Successful());
    }

    private sealed class DiagnosticStubSession
        : StubSession,
          IRuntimeHostDiagnosticSource
    {
        public DiagnosticStubSession(
            IReadOnlyList<RemoteObservationState> states)
            : base(states)
        {
        }

        public event EventHandler<RemoteRuntimeDiagnosticObservedEventArgs>?
            DiagnosticObserved;
        public event EventHandler<RemoteRuntimeDiagnosticStreamFaultedEventArgs>?
            DiagnosticStreamFaulted;

        public RemoteRuntimeDiagnosticObservedEventArgs PublishDiagnostic(
            RemoteRuntimeDiagnosticObservation observation)
        {
            var args = new RemoteRuntimeDiagnosticObservedEventArgs(observation);
            DiagnosticObserved?.Invoke(this, args);
            return args;
        }

        public RemoteRuntimeDiagnosticStreamFaultedEventArgs FailDiagnostics(
            RemoteRuntimeDiagnosticStreamFailureKind kind,
            Exception exception)
        {
            var args = new RemoteRuntimeDiagnosticStreamFaultedEventArgs(
                kind,
                exception);
            DiagnosticStreamFaulted?.Invoke(this, args);
            return args;
        }
    }
}
