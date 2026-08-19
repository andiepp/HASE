using Hase.Runtime.Media;

namespace Hase.Runtime.Media.Tests;

public sealed class RuntimeHostMediaInventoryReconcilerTests
{
    private static readonly byte[] IdentityKey =
        Enumerable.Range(0, 32).Select(index => (byte)index).ToArray();

    [Fact]
    public void UnknownCameraReceivesOpaqueStableIdentityAndVideoOnlyCapability()
    {
        var first = new RuntimeHostMediaInventoryReconciler([], IdentityKey);
        var second = new RuntimeHostMediaInventoryReconciler([], IdentityKey);

        RuntimeHostMediaSourceConfiguration source = Assert.Single(
            first.Reconcile([new("browser-device-secret")]));
        RuntimeHostMediaSourceConfiguration afterRestart = Assert.Single(
            second.Reconcile([new("browser-device-secret")]));

        Assert.StartsWith("camera-", source.Target.MediaSourceId);
        Assert.Equal(source.Target.MediaSourceId,
            afterRestart.Target.MediaSourceId);
        Assert.DoesNotContain("browser-device-secret",
            source.Target.MediaSourceId);
        Assert.False(source.SupportsAudio);
    }

    [Fact]
    public void ReplugPreservesLogicalIdentityButCreatesNewGeneration()
    {
        var reconciler = new RuntimeHostMediaInventoryReconciler([], IdentityKey);
        RuntimeHostMediaSourceConfiguration first = Assert.Single(
            reconciler.Reconcile([new("camera-device")]));

        Assert.Empty(reconciler.Reconcile([]));
        RuntimeHostMediaSourceConfiguration replugged = Assert.Single(
            reconciler.Reconcile([new("camera-device")]));

        Assert.Equal(first.Target.MediaSourceId,
            replugged.Target.MediaSourceId);
        Assert.NotEqual(first.Target.MediaSourceGeneration,
            replugged.Target.MediaSourceGeneration);
    }

    [Fact]
    public void ConfiguredAliasAndMicrophoneAreRetained()
    {
        var alias = new RuntimeHostMediaSourceConfiguration(
            new("operator-camera", "ignored-generation"),
            "camera-device",
            "microphone-device",
            RuntimeHostMediaSourceAvailability.Idle,
            "Workbench camera");
        var reconciler = new RuntimeHostMediaInventoryReconciler(
            [alias],
            IdentityKey);

        RuntimeHostMediaSourceConfiguration source = Assert.Single(
            reconciler.Reconcile([new("camera-device")]));

        Assert.Equal("operator-camera", source.Target.MediaSourceId);
        Assert.Equal("Workbench camera", source.DisplayName);
        Assert.True(source.SupportsAudio);
    }

    [Fact]
    public async Task RemovingActiveCameraFaultsOnlyMediaSessionAsSourceLost()
    {
        var boundary = new RecordingBoundary();
        var source = new RuntimeHostMediaSourceConfiguration(
            new("camera", "generation"),
            "device",
            null,
            RuntimeHostMediaSourceAvailability.Idle,
            "Camera");
        await using var owner = new RuntimeHostMediaSessionOwner(
            [source], boundary);
        RuntimeHostMediaOperationResult started = await owner.StartAsync(
            new("principal", source.Target, IncludeAudio: false));

        await owner.ReplaceSourcesAsync([]);
        RuntimeHostMediaOperationResult status = await owner.GetStatusAsync(
            "principal",
            started.Session!.SessionId);

        Assert.Equal(RuntimeHostMediaSessionState.Faulted,
            status.Session!.State);
        Assert.Equal(RuntimeHostMediaTerminalReason.SourceLost,
            status.Session.TerminalReason);
        Assert.Equal(1, boundary.CloseCount);
    }

    [Fact]
    public async Task CapabilityWatchPublishesInitialAndChangedCompleteSnapshots()
    {
        var boundary = new RecordingBoundary();
        await using var owner = new RuntimeHostMediaSessionOwner(
            [],
            boundary,
            allowEmptySources: true);
        await using IAsyncEnumerator<RuntimeHostMediaCapabilitySnapshot>
            stream = owner.WatchCapabilitiesAsync().GetAsyncEnumerator();

        Assert.True(await stream.MoveNextAsync());
        RuntimeHostMediaCapabilitySnapshot initial = stream.Current;
        var source = new RuntimeHostMediaSourceConfiguration(
            new("camera", "generation"),
            "device",
            null,
            RuntimeHostMediaSourceAvailability.Idle,
            "Camera");
        await owner.ReplaceSourcesAsync([source]);
        Assert.True(await stream.MoveNextAsync());

        Assert.Empty(initial.Sources);
        Assert.True(stream.Current.Revision > initial.Revision);
        Assert.Equal(source, Assert.Single(stream.Current.Sources));
    }

    private sealed class RecordingBoundary : IRuntimeHostMediaCaptureBoundary
    {
        public int CloseCount { get; private set; }

        public ValueTask OpenAsync(
            RuntimeHostMediaSourceConfiguration source,
            bool includeAudio,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask SubmitNegotiationAsync(
            RuntimeHostMediaNegotiationMessage message,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask CloseAsync(CancellationToken cancellationToken)
        {
            CloseCount++;
            return ValueTask.CompletedTask;
        }
    }
}
