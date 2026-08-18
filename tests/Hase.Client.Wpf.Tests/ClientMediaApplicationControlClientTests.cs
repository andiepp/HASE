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

    private static ClientMediaApplicationControlClient Create(
        FakeRemoteClient remote,
        FakeBoundary boundary) =>
        new(_ => remote, boundary, new SynchronizationContext());

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

        public Task<RemoteMediaExchangeResult> ExchangeAsync(
            string sessionId, uint acknowledgedDeliverySequence,
            RemoteMediaNegotiationMessage? submittedMessage,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RemoteMediaExchangeResult(true,
                new(sessionId, new("camera", "generation"), false,
                    RemoteMediaSessionState.Negotiating),
                null, 0, [], false));

        public Task<RemoteMediaStatusResult> GetStatusAsync(
            string sessionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RemoteMediaStatusResult(true,
                new(sessionId, new("camera", "generation"), false,
                    RemoteMediaSessionState.Streaming), null));

        public Task<RemoteMediaStopResult> StopAsync(
            string sessionId, CancellationToken cancellationToken = default)
        {
            StopCount++;
            return Task.FromResult(new RemoteMediaStopResult(true,
                new(sessionId, new("camera", "generation"), false,
                    RemoteMediaSessionState.Ended), null));
        }
    }
}
