using Hase.Client;
using Hase.Client.Configuration;
using Hase.Client.Diagnostics;
using Hase.Client.Media;
using Hase.Client.Wpf.AppHost.Media;

namespace Hase.Client.Wpf.Tests;

public sealed class ClientMediaApplicationControlClientTests
{
    [Fact]
    public async Task CapabilitiesDoNotInitializePresentation()
    {
        var remote = new FakeRemoteClient();
        var boundary = new FakeBoundary();
        await using var client = Create(remote, boundary);
        client.SelectRuntimeHost(new RuntimeHostProfileId("host"));

        IReadOnlyList<RemoteMediaSourceCapability> capabilities =
            await client.GetCapabilitiesAsync();

        Assert.Single(capabilities);
        Assert.Equal(0, boundary.BeginCount);
    }

    [Fact]
    public async Task CapabilityFailurePublishesOnlyNormalizedDiagnostic()
    {
        var remote = new FakeRemoteClient
        {
            CapabilitiesFailure = new RuntimeHostClientException(
                RuntimeHostClientFailureCategory.TransportUnavailable,
                "The Runtime Host is unavailable.",
                new InvalidOperationException("sensitive raw transport detail"))
        };
        var boundary = new FakeBoundary();
        var collector = new BoundedClientDiagnosticCollector(10);
        var diagnostics = new ClientDiagnosticPublisher(collector);
        await using var client = new ClientMediaApplicationControlClient(
            _ => remote,
            boundary,
            new SynchronizationContext(),
            diagnostics);
        client.SelectRuntimeHost(new RuntimeHostProfileId("host"));

        RuntimeHostClientException exception = await Assert.ThrowsAsync<
            RuntimeHostClientException>(() => client.GetCapabilitiesAsync());

        Assert.Equal(RuntimeHostClientFailureCategory.TransportUnavailable,
            exception.Category);
        ClientDiagnosticRecord record = Assert.Single(
            collector.GetSnapshot().Records);
        Assert.Equal("MediaCapabilitiesRefreshFailed", record.EventName);
        Assert.Equal(ClientDiagnosticCategory.ClientPresentation,
            record.Category);
        Assert.Equal(ClientDiagnosticSeverity.Warning, record.Severity);
        Assert.Equal(ClientDiagnosticOutcome.Failed, record.Outcome);
        Assert.Equal("TransportUnavailable",
            record.Metadata["failureCategory"]);
        Assert.Equal("The Runtime Host is unavailable.",
            record.Metadata["safeMessage"]);
        Assert.DoesNotContain("sensitive",
            string.Join(" ", record.Metadata.Values),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExplicitStartAndStopOwnPresentationLifecycle()
    {
        var remote = new FakeRemoteClient();
        var boundary = new FakeBoundary();
        await using var client = Create(remote, boundary);
        client.SelectRuntimeHost(new RuntimeHostProfileId("host"));

        RemoteMediaStartResult started = await client.StartAsync(
            new("camera", "generation"), includeAudio: true);
        RemoteMediaStopResult stopped = await client.StopAsync(
            started.Session!.SessionId);

        Assert.True(started.Succeeded);
        Assert.True(stopped.Succeeded);
        Assert.Equal(1, boundary.BeginCount);
        Assert.True(boundary.IncludeAudio);
        Assert.True(boundary.ClearCount >= 1);
        Assert.Equal(1, remote.StopCount);
    }

    [Fact]
    public async Task PresentationFaultPublishesOnlyNormalizedDiagnostic()
    {
        var remote = new FakeRemoteClient();
        var boundary = new FakeBoundary();
        var collector = new BoundedClientDiagnosticCollector(10);
        var diagnostics = new ClientDiagnosticPublisher(collector);
        await using var client = new ClientMediaApplicationControlClient(
            _ => remote,
            boundary,
            new SynchronizationContext(),
            diagnostics);
        client.SelectRuntimeHost(new RuntimeHostProfileId("host"));

        boundary.Publish(new(
            ClientMediaWebMessageKind.PresentationFaulted,
            "browser-failed"));

        ClientDiagnosticRecord record = Assert.Single(
            collector.GetSnapshot().Records);
        Assert.Equal("MediaBoundaryFaulted", record.EventName);
        Assert.Equal(ClientDiagnosticCategory.ClientPresentation,
            record.Category);
        Assert.Equal(ClientDiagnosticSeverity.Warning, record.Severity);
        Assert.Equal(ClientDiagnosticOutcome.Failed, record.Outcome);
        Assert.Equal("browser-failed", record.Metadata["failureCategory"]);
        Assert.Single(record.Metadata);
    }

    [Fact]
    public async Task HostSelectionChangeClearsWithoutReplayingStart()
    {
        var remote = new FakeRemoteClient();
        var boundary = new FakeBoundary();
        await using var client = Create(remote, boundary);
        client.SelectRuntimeHost(new RuntimeHostProfileId("first"));
        await client.StartAsync(new("camera", "generation"), false);

        client.SelectRuntimeHost(new RuntimeHostProfileId("second"));

        Assert.Equal(1, remote.StartCount);
        Assert.True(boundary.ClearCount >= 1);
    }

    [Fact]
    public async Task DisconnectAndReconnectClearWithoutReplayingStart()
    {
        var remote = new FakeRemoteClient();
        var boundary = new FakeBoundary();
        await using var client = Create(remote, boundary);
        var profileId = new RuntimeHostProfileId("host");
        client.SelectRuntimeHost(profileId);
        await client.StartAsync(new("camera", "generation"), false);

        client.NotifyRuntimeHostState(
            profileId, RuntimeHostClientSessionState.Reconnecting);
        client.NotifyRuntimeHostState(
            profileId, RuntimeHostClientSessionState.Connected);

        Assert.Equal(1, remote.StartCount);
        Assert.Equal(1, boundary.BeginCount);
        Assert.True(boundary.ClearCount >= 1);
        Assert.Equal(1, remote.StopCount);
    }

    [Fact]
    public async Task NegotiationFailureStopsRemoteOnceAndPublishesCategory()
    {
        var remote = new FakeRemoteClient
        {
            ExchangeResult = new(false, null, "timed-out", 0, [], false)
        };
        var boundary = new FakeBoundary();
        var collector = new BoundedClientDiagnosticCollector(10);
        var diagnostics = new ClientDiagnosticPublisher(collector);
        await using var client = new ClientMediaApplicationControlClient(
            _ => remote,
            boundary,
            new ImmediateSynchronizationContext(),
            diagnostics);
        client.SelectRuntimeHost(new RuntimeHostProfileId("host"));
        var failureObserved = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        client.SessionChanged += (_, args) =>
        {
            if (args.Session is null &&
                args.StatusText == "Media negotiation failed.")
            {
                failureObserved.TrySetResult();
            }
        };

        await client.StartAsync(new("camera", "generation"), false);
        await failureObserved.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await client.DisposeAsync();

        Assert.Equal(1, remote.StopCount);
        ClientDiagnosticRecord record = Assert.Single(
            collector.GetSnapshot().Records);
        Assert.Equal("MediaNegotiationExchangeFailed", record.EventName);
        Assert.Equal(ClientDiagnosticCategory.ClientPresentation,
            record.Category);
        Assert.Equal(ClientDiagnosticSeverity.Warning, record.Severity);
        Assert.Equal(ClientDiagnosticOutcome.Failed, record.Outcome);
        Assert.Equal("timed-out", record.Metadata["failureCategory"]);
        Assert.Single(record.Metadata);
        Assert.Null(record.EndpointId);
        Assert.Null(record.InstrumentId);
        Assert.Null(record.OperationId);
    }

    [Fact]
    public async Task NegotiationCleanupFailureIsSanitizedAndStillClearsLocalState()
    {
        var remote = new FakeRemoteClient
        {
            ExchangeResult = new(false, null, "invalid-state", 0, [], false),
            StopFailure = new RuntimeHostClientException(
                RuntimeHostClientFailureCategory.TransportUnavailable,
                "safe",
                new InvalidOperationException(
                    "sensitive session device SDP ICE credential"))
        };
        var boundary = new FakeBoundary();
        var collector = new BoundedClientDiagnosticCollector(10);
        var diagnostics = new ClientDiagnosticPublisher(collector);
        await using var client = new ClientMediaApplicationControlClient(
            _ => remote,
            boundary,
            new ImmediateSynchronizationContext(),
            diagnostics);
        client.SelectRuntimeHost(new RuntimeHostProfileId("host"));
        var failureObserved = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        client.SessionChanged += (_, args) =>
        {
            if (args.Session is null &&
                args.StatusText == "Media negotiation failed.")
            {
                failureObserved.TrySetResult();
            }
        };

        await client.StartAsync(new("camera", "generation"), false);
        await failureObserved.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await client.DisposeAsync();

        Assert.Equal(1, remote.StopCount);
        IReadOnlyList<ClientDiagnosticRecord> records =
            collector.GetSnapshot().Records;
        Assert.Equal(2, records.Count);
        ClientDiagnosticRecord exchange = Assert.Single(records,
            record => record.EventName == "MediaNegotiationExchangeFailed");
        ClientDiagnosticRecord cleanup = Assert.Single(records,
            record => record.EventName == "MediaSessionCleanupFailed");
        Assert.Equal("invalid-state",
            exchange.Metadata["failureCategory"]);
        Assert.Equal("TransportUnavailable",
            cleanup.Metadata["failureCategory"]);
        Assert.Single(exchange.Metadata);
        Assert.Single(cleanup.Metadata);
        Assert.DoesNotContain("sensitive",
            string.Join(" ", records.SelectMany(
                record => record.Metadata.Values)),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnboundedFailureCategoryIsNotPublished()
    {
        var remote = new FakeRemoteClient
        {
            ExchangeResult = new(false, null,
                "sensitive value with spaces and device identity",
                0, [], false)
        };
        var boundary = new FakeBoundary();
        var collector = new BoundedClientDiagnosticCollector(10);
        var diagnostics = new ClientDiagnosticPublisher(collector);
        await using var client = new ClientMediaApplicationControlClient(
            _ => remote,
            boundary,
            new ImmediateSynchronizationContext(),
            diagnostics);
        client.SelectRuntimeHost(new RuntimeHostProfileId("host"));
        var failureObserved = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        client.SessionChanged += (_, args) =>
        {
            if (args.Session is null)
            {
                failureObserved.TrySetResult();
            }
        };

        await client.StartAsync(new("camera", "generation"), false);
        await failureObserved.Task.WaitAsync(TimeSpan.FromSeconds(1));

        ClientDiagnosticRecord record = Assert.Single(
            collector.GetSnapshot().Records);
        Assert.Equal("unspecified", record.Metadata["failureCategory"]);
        Assert.Equal(1, remote.StopCount);
    }

    private static ClientMediaApplicationControlClient Create(
        FakeRemoteClient remote,
        FakeBoundary boundary) =>
        new(_ => remote, boundary, new ImmediateSynchronizationContext());

    private sealed class ImmediateSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state) =>
            callback(state);
    }

    private sealed class FakeBoundary : IClientMediaPresentationBoundary
    {
        public event Action<ClientMediaWebMessage>? ValidatedMessage;
        public int BeginCount { get; private set; }
        public int ClearCount { get; private set; }
        public bool IncludeAudio { get; private set; }

        public Task BeginAsync(bool includeAudio,
            CancellationToken cancellationToken = default)
        {
            BeginCount++;
            IncludeAudio = includeAudio;
            return Task.CompletedTask;
        }

        public void SubmitNegotiation(RemoteMediaNegotiationMessage message)
        {
        }

        public void ClearPresentation() => ClearCount++;
        public void Publish(ClientMediaWebMessage message) =>
            ValidatedMessage?.Invoke(message);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeRemoteClient : IRuntimeHostMediaControlClient
    {
        public Exception? CapabilitiesFailure { get; init; }
        public RemoteMediaExchangeResult? ExchangeResult { get; init; }
        public Exception? StopFailure { get; init; }
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }

        public Task<IReadOnlyList<RemoteMediaSourceCapability>>
            GetCapabilitiesAsync(CancellationToken cancellationToken = default) =>
            CapabilitiesFailure is null
                ? Task.FromResult<IReadOnlyList<RemoteMediaSourceCapability>>(
                    [new(new("camera", "generation"), "Camera",
                        RemoteMediaSourceAvailability.Idle, true, true)])
                : Task.FromException<IReadOnlyList<RemoteMediaSourceCapability>>(
                    CapabilitiesFailure);

        public Task<RemoteMediaStartResult> StartAsync(
            RemoteMediaSourceTarget target, bool includeAudio,
            CancellationToken cancellationToken = default)
        {
            StartCount++;
            return Task.FromResult(new RemoteMediaStartResult(true,
                new("session", target, includeAudio,
                    RemoteMediaSessionState.Negotiating), null));
        }

        public async Task<RemoteMediaExchangeResult> ExchangeAsync(
            string sessionId, uint acknowledgedDeliverySequence,
            RemoteMediaNegotiationMessage? submittedMessage,
            CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            return ExchangeResult ?? new RemoteMediaExchangeResult(true,
                new(sessionId, new("camera", "generation"), false,
                    RemoteMediaSessionState.Negotiating),
                null, 0, [], false);
        }

        public Task<RemoteMediaStatusResult> GetStatusAsync(
            string sessionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RemoteMediaStatusResult(true,
                new(sessionId, new("camera", "generation"), false,
                    RemoteMediaSessionState.Streaming), null));

        public Task<RemoteMediaStopResult> StopAsync(
            string sessionId, CancellationToken cancellationToken = default)
        {
            StopCount++;
            if (StopFailure is not null)
            {
                return Task.FromException<RemoteMediaStopResult>(StopFailure);
            }
            return Task.FromResult(new RemoteMediaStopResult(true,
                new(sessionId, new("camera", "generation"), false,
                    RemoteMediaSessionState.Ended), null));
        }
    }
}
