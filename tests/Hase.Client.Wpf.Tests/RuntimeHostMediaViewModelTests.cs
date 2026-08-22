using Hase.Client;
using Hase.Client.Media;
using Hase.Client.Wpf.ViewModels;

namespace Hase.Client.Wpf.Tests;

public sealed class RuntimeHostMediaViewModelTests
{
    [Fact]
    public async Task RefreshOrdersSanitizedLogicalSourcesWithoutAutoSelectingMany()
    {
        var client = new FakeClient
        {
            Capabilities =
            [
                Source("usb", "USB camera", supportsAudio: true),
                Source("built-in", "Built-in camera", supportsAudio: false)
            ]
        };
        var viewModel = Create(client);

        await viewModel.RefreshAsync();

        Assert.Equal(["Built-in camera", "USB camera"],
            viewModel.Sources.Select(item => item.DisplayName));
        Assert.Null(viewModel.SelectedSource);
        Assert.False(viewModel.StartCommand.CanExecute());
    }

    [Fact]
    public async Task SingleSourceIsSelectedButNeverStartedAutomatically()
    {
        var client = new FakeClient
        {
            Capabilities = [Source("built-in", "Built-in camera", false)]
        };
        var viewModel = Create(client);

        await viewModel.RefreshAsync();

        Assert.Equal("built-in", viewModel.SelectedSource!.Target.MediaSourceId);
        Assert.True(viewModel.StartCommand.CanExecute());
        Assert.Empty(client.StartRequests);
    }

    [Fact]
    public async Task StartUsesExactSelectedGenerationAndIndependentAudioChoice()
    {
        var selected = Source("usb", "USB camera", supportsAudio: true);
        var client = new FakeClient { Capabilities = [selected] };
        var viewModel = Create(client);
        await viewModel.RefreshAsync();
        viewModel.IncludeAudio = true;

        await viewModel.StartAsync();

        var request = Assert.Single(client.StartRequests);
        Assert.Equal(selected.Target, request.Target);
        Assert.True(request.IncludeAudio);
        Assert.False(viewModel.StartCommand.CanExecute());
        Assert.True(viewModel.StopCommand.CanExecute());
    }

    [Fact]
    public async Task AudioCannotBeRequestedForVideoOnlySource()
    {
        var client = new FakeClient
        {
            Capabilities = [Source("built-in", "Built-in camera", false)]
        };
        var viewModel = Create(client);
        await viewModel.RefreshAsync();

        viewModel.IncludeAudio = true;

        Assert.False(viewModel.IncludeAudio);
        Assert.False(viewModel.CanRequestAudio);
    }

    [Fact]
    public async Task SourceCannotChangeUntilExplicitStopCompletes()
    {
        var first = Source("built-in", "Built-in camera", false);
        var second = Source("usb", "USB camera", true);
        var client = new FakeClient { Capabilities = [first, second] };
        var viewModel = Create(client);
        await viewModel.RefreshAsync();
        viewModel.SelectedSource = viewModel.Sources[0];
        await viewModel.StartAsync();

        viewModel.SelectedSource = viewModel.Sources[1];
        await viewModel.StopAsync();

        Assert.Equal("built-in", viewModel.SelectedSource!.Target.MediaSourceId);
        Assert.Single(client.StopSessionIds);
        Assert.True(viewModel.StartCommand.CanExecute());
    }

    [Fact]
    public async Task RuntimeHostChangeDropsCapabilitiesAndNeverReplaysStart()
    {
        var client = new FakeClient
        {
            Capabilities = [Source("usb", "USB camera", true)]
        };
        var viewModel = Create(client);
        await viewModel.RefreshAsync();

        viewModel.ResetForRuntimeHostChange();

        Assert.Empty(viewModel.Sources);
        Assert.Null(viewModel.SelectedSource);
        Assert.False(viewModel.IncludeAudio);
        Assert.Empty(client.StartRequests);
    }

    [Fact]
    public async Task RuntimeHostChangeDuringActiveSessionPreservesSession()
    {
        var client = new FakeClient
        {
            Capabilities = [Source("usb", "USB camera", true)]
        };
        var viewModel = Create(client);
        await viewModel.RefreshAsync();
        await viewModel.StartAsync();

        viewModel.ResetForRuntimeHostChange();

        Assert.NotEmpty(viewModel.Sources);
        Assert.NotNull(viewModel.SelectedSource);
        Assert.True(viewModel.StopCommand.CanExecute());

        await viewModel.StopAsync();
        viewModel.ResetForRuntimeHostChange();

        Assert.Empty(viewModel.Sources);
        Assert.Null(viewModel.SelectedSource);
    }

    [Fact]
    public async Task StaleSelectionFailureRequiresRefreshWithoutFallback()
    {
        var client = new FakeClient
        {
            Capabilities = [Source("usb", "USB camera", true)],
            StartFailureCode = "source-not-current"
        };
        var viewModel = Create(client);
        await viewModel.RefreshAsync();

        await viewModel.StartAsync();

        Assert.Equal("The camera selection is stale. Refresh cameras.",
            viewModel.StatusText);
        Assert.Single(client.StartRequests);
        Assert.False(viewModel.StopCommand.CanExecute());
    }

    [Fact]
    public async Task SourceLossRefreshesInventoryAndSelectsSoleRemainingCamera()
    {
        var first = Source("first", "First camera", false);
        var removed = Source("removed", "Removed camera", true);
        var client = new FakeClient
        {
            Capabilities = [first, removed]
        };
        var viewModel = Create(client);
        await viewModel.RefreshAsync();
        viewModel.SelectedSource = Assert.Single(
            viewModel.Sources,
            source => source.Target == removed.Target);
        await viewModel.StartAsync();
        client.Capabilities = [first];

        client.PublishSessionChanged(
            null,
            "Media stopped because the camera was disconnected.",
            RemoteMediaTerminalReason.SourceLost);

        RuntimeHostMediaSourceItemViewModel remaining =
            Assert.Single(viewModel.Sources);
        Assert.Equal(first.Target, remaining.Target);
        Assert.Equal(first.Target, viewModel.SelectedSource!.Target);
        Assert.True(viewModel.StartCommand.CanExecute());
        Assert.Single(client.StartRequests);
        Assert.Equal(
            "Media stopped because the camera was disconnected.",
            viewModel.StatusText);
    }

    [Fact]
    public async Task SourceLossRefreshFailureClearsStaleSelectionWithoutRestart()
    {
        var removed = Source("removed", "Removed camera", true);
        var client = new FakeClient
        {
            Capabilities = [removed]
        };
        var viewModel = Create(client);
        await viewModel.RefreshAsync();
        await viewModel.StartAsync();
        client.CapabilitiesFailure = new InvalidOperationException("failed");

        client.PublishSessionChanged(
            null,
            "Media stopped because the camera was disconnected.",
            RemoteMediaTerminalReason.SourceLost);

        Assert.Null(viewModel.SelectedSource);
        Assert.False(viewModel.StartCommand.CanExecute());
        Assert.Single(client.StartRequests);
        Assert.Equal(
            "Media stopped because the camera was disconnected. "
            + "Refresh Cameras to recover the current inventory.",
            viewModel.StatusText);
    }

    [Fact]
    public async Task RefreshShowsOnlyNormalizedFailureCategoryAndSafeMessage()
    {
        var client = new FakeClient
        {
            CapabilitiesFailure = new RuntimeHostClientException(
                RuntimeHostClientFailureCategory.Authorization,
                "The runtime-host client is not authorized.",
                new InvalidOperationException("sensitive raw transport detail"))
        };
        var viewModel = Create(client);

        await viewModel.RefreshAsync();

        Assert.Empty(viewModel.Sources);
        Assert.Null(viewModel.SelectedSource);
        Assert.Equal(
            "Camera capabilities failed (Authorization): "
            + "The runtime-host client is not authorized.",
            viewModel.StatusText);
        Assert.DoesNotContain("sensitive", viewModel.StatusText,
            StringComparison.OrdinalIgnoreCase);
    }

    private static RuntimeHostMediaViewModel Create(FakeClient client)
    {
        var viewModel = new RuntimeHostMediaViewModel();
        viewModel.Configure(client);
        return viewModel;
    }

    private static RemoteMediaSourceCapability Source(
        string id,
        string displayName,
        bool supportsAudio) =>
        new(
            new(id, "generation-01"),
            displayName,
            RemoteMediaSourceAvailability.Idle,
            SupportsVideo: true,
            SupportsAudio: supportsAudio);

    private sealed class FakeClient :
        IRuntimeHostMediaControlClient,
        IRuntimeHostMediaSessionNotifications
    {
        public event EventHandler<RemoteMediaSessionChangedEventArgs>?
            SessionChanged;
        public IReadOnlyList<RemoteMediaSourceCapability> Capabilities { get; set; } = [];
        public Exception? CapabilitiesFailure { get; set; }
        public string? StartFailureCode { get; init; }
        public List<(RemoteMediaSourceTarget Target, bool IncludeAudio)> StartRequests { get; } = [];
        public List<string> StopSessionIds { get; } = [];

        public Task<IReadOnlyList<RemoteMediaSourceCapability>> GetCapabilitiesAsync(
            CancellationToken cancellationToken = default) =>
            CapabilitiesFailure is null
                ? Task.FromResult(Capabilities)
                : Task.FromException<IReadOnlyList<RemoteMediaSourceCapability>>(
                    CapabilitiesFailure);

        public Task<RemoteMediaStartResult> StartAsync(
            RemoteMediaSourceTarget target,
            bool includeAudio,
            CancellationToken cancellationToken = default)
        {
            StartRequests.Add((target, includeAudio));
            return Task.FromResult(StartFailureCode is null
                ? new RemoteMediaStartResult(
                    true,
                    new("session-01", target, includeAudio,
                        RemoteMediaSessionState.Starting),
                    null)
                : new RemoteMediaStartResult(false, null, StartFailureCode));
        }

        public Task<RemoteMediaExchangeResult> ExchangeAsync(
            string sessionId,
            uint acknowledgedDeliverySequence,
            RemoteMediaNegotiationMessage? submittedMessage,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RemoteMediaExchangeResult(
                true,
                null,
                null,
                submittedMessage?.Sequence ?? 0,
                [],
                false));

        public Task<RemoteMediaStatusResult> GetStatusAsync(
            string sessionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RemoteMediaStatusResult(true, null, null));

        public Task<RemoteMediaStopResult> StopAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            StopSessionIds.Add(sessionId);
            return Task.FromResult(new RemoteMediaStopResult(true, null, null));
        }

        public void PublishSessionChanged(
            RemoteMediaSessionSnapshot? session,
            string statusText,
            RemoteMediaTerminalReason terminalReason) =>
            SessionChanged?.Invoke(
                this,
                new RemoteMediaSessionChangedEventArgs(
                    session,
                    statusText,
                    terminalReason));
    }
}
