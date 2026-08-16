using Hase.Runtime.Media;

namespace Hase.Runtime.Media.Tests;

public sealed class RuntimeHostMediaSessionOwnerTests
{
    [Fact]
    public async Task StartOpensOnlyExactConfiguredSourceAndEntersNegotiating()
    {
        var boundary = new RecordingBoundary();
        await using var owner = CreateOwner(boundary);

        var result = await owner.StartAsync(Request());

        Assert.Equal(RuntimeHostMediaOperationStatus.Success, result.Status);
        Assert.Equal(RuntimeHostMediaSessionState.Negotiating, result.Session!.State);
        Assert.Equal(Target(), result.Session.Target);
        Assert.Single(boundary.Opened);
        Assert.False(boundary.Opened[0].IncludeAudio);
    }

    [Fact]
    public async Task StaleGenerationIsRejectedBeforeOpeningBoundary()
    {
        var boundary = new RecordingBoundary();
        await using var owner = CreateOwner(boundary);

        var result = await owner.StartAsync(
            Request(target: new("camera-01", "stale")));

        Assert.Equal(RuntimeHostMediaOperationStatus.SourceNotCurrent, result.Status);
        Assert.Empty(boundary.Opened);
    }

    [Theory]
    [InlineData(RuntimeHostMediaSourceAvailability.Unavailable)]
    [InlineData(RuntimeHostMediaSourceAvailability.Busy)]
    [InlineData(RuntimeHostMediaSourceAvailability.Faulted)]
    public async Task NonIdleSourceIsRejectedWithoutFallback(
        RuntimeHostMediaSourceAvailability availability)
    {
        var boundary = new RecordingBoundary();
        await using var owner = CreateOwner(boundary, availability: availability);

        var result = await owner.StartAsync(Request());

        Assert.Equal(RuntimeHostMediaOperationStatus.SourceUnavailable, result.Status);
        Assert.Empty(boundary.Opened);
    }

    [Fact]
    public async Task AudioRequiresExactConfiguredAudioDevice()
    {
        var boundary = new RecordingBoundary();
        await using var owner = CreateOwner(boundary, audioDeviceId: null);

        var result = await owner.StartAsync(Request(includeAudio: true));

        Assert.Equal(RuntimeHostMediaOperationStatus.AudioNotSupported, result.Status);
        Assert.Empty(boundary.Opened);
    }

    [Fact]
    public async Task SecondSessionDoesNotDisturbFirst()
    {
        var boundary = new RecordingBoundary();
        await using var owner = CreateOwner(boundary);
        var first = await owner.StartAsync(Request());

        var second = await owner.StartAsync(Request(principal: "principal-02"));
        var status = await owner.GetStatusAsync(
            "principal-01",
            first.Session!.SessionId);

        Assert.Equal(RuntimeHostMediaOperationStatus.SessionBusy, second.Status);
        Assert.Equal(RuntimeHostMediaSessionState.Negotiating, status.Session!.State);
        Assert.Equal(0, boundary.CloseCount);
    }

    [Fact]
    public async Task ExplicitSecondLogicalCameraOpensItsExactLocalBinding()
    {
        var boundary = new RecordingBoundary();
        IReadOnlyList<RuntimeHostMediaSourceConfiguration> sources =
        [
            new(Target(), "camera-device-local", null,
                RuntimeHostMediaSourceAvailability.Idle, "Built-in camera"),
            new(new("usb-camera", "generation-02"),
                "usb-camera-device-local", "usb-microphone-device-local",
                RuntimeHostMediaSourceAvailability.Idle, "USB camera")
        ];
        await using var owner = new RuntimeHostMediaSessionOwner(
            sources,
            boundary);

        var result = await owner.StartAsync(
            Request(
                target: new("usb-camera", "generation-02"),
                includeAudio: true));

        Assert.Equal(RuntimeHostMediaOperationStatus.Success, result.Status);
        Assert.Equal("usb-camera", result.Session!.Target.MediaSourceId);
        Assert.Equal("usb-camera-device-local",
            Assert.Single(boundary.Opened).Source.VideoDeviceId);
        Assert.True(boundary.Opened[0].IncludeAudio);
    }

    [Fact]
    public async Task UnknownCameraDoesNotFallBackToAnotherConfiguredSource()
    {
        var boundary = new RecordingBoundary();
        await using var owner = CreateOwner(boundary);

        var result = await owner.StartAsync(
            Request(target: new("unknown-camera", "generation-01")));

        Assert.Equal(RuntimeHostMediaOperationStatus.SourceNotCurrent,
            result.Status);
        Assert.Empty(boundary.Opened);
    }

    [Fact]
    public void DuplicateLogicalSourceIdsAreRejected()
    {
        IReadOnlyList<RuntimeHostMediaSourceConfiguration> sources =
        [
            new(Target(), "device-01", null,
                RuntimeHostMediaSourceAvailability.Idle, "Camera one"),
            new(new("camera-01", "generation-02"), "device-02", null,
                RuntimeHostMediaSourceAvailability.Idle, "Camera two")
        ];

        Assert.Throws<ArgumentException>(() =>
            new RuntimeHostMediaSessionOwner(sources, new RecordingBoundary()));
    }

    [Fact]
    public async Task SessionOperationsRequireOwningPrincipal()
    {
        var boundary = new RecordingBoundary();
        await using var owner = CreateOwner(boundary);
        var start = await owner.StartAsync(Request());

        var result = await owner.StopAsync(
            "principal-02",
            start.Session!.SessionId);

        Assert.Equal(RuntimeHostMediaOperationStatus.SessionNotOwned, result.Status);
        Assert.Equal(0, boundary.CloseCount);
    }

    [Fact]
    public async Task NegotiationMustBeStrictlySequenced()
    {
        var boundary = new RecordingBoundary();
        await using var owner = CreateOwner(boundary);
        var start = await owner.StartAsync(Request());

        var rejected = await owner.ExchangeAsync(
            "principal-01",
            start.Session!.SessionId,
            new(2, RuntimeHostMediaNegotiationKind.Answer, "answer"));
        var accepted = await owner.ExchangeAsync(
            "principal-01",
            start.Session.SessionId,
            new(1, RuntimeHostMediaNegotiationKind.Answer, "answer"));

        Assert.Equal(RuntimeHostMediaOperationStatus.InvalidRequest, rejected.Status);
        Assert.Equal(RuntimeHostMediaOperationStatus.Success, accepted.Status);
        Assert.Single(boundary.Submitted);
    }

    [Theory]
    [InlineData(RuntimeHostMediaNegotiationKind.Offer, "")]
    [InlineData(RuntimeHostMediaNegotiationKind.Answer, "")]
    [InlineData(RuntimeHostMediaNegotiationKind.IceCandidate, "")]
    [InlineData(RuntimeHostMediaNegotiationKind.IceComplete, "payload")]
    public async Task InvalidNegotiationPayloadIsRejected(
        RuntimeHostMediaNegotiationKind kind,
        string payload)
    {
        var boundary = new RecordingBoundary();
        await using var owner = CreateOwner(boundary);
        var start = await owner.StartAsync(Request());

        var result = await owner.ExchangeAsync(
            "principal-01",
            start.Session!.SessionId,
            new(1, kind, payload));

        Assert.Equal(RuntimeHostMediaOperationStatus.InvalidRequest, result.Status);
        Assert.Empty(boundary.Submitted);
    }

    [Fact]
    public async Task StopIsIdempotentAndReleasesBoundaryExactlyOnce()
    {
        var boundary = new RecordingBoundary();
        await using var owner = CreateOwner(boundary);
        var start = await owner.StartAsync(Request());

        var first = await owner.StopAsync(
            "principal-01",
            start.Session!.SessionId);
        var second = await owner.StopAsync(
            "principal-01",
            start.Session.SessionId);

        Assert.Equal(RuntimeHostMediaOperationStatus.Success, first.Status);
        Assert.Equal(RuntimeHostMediaOperationStatus.Success, second.Status);
        Assert.Equal(RuntimeHostMediaSessionState.Ended, second.Session!.State);
        Assert.Equal(RuntimeHostMediaTerminalReason.ClientStopped,
            second.Session.TerminalReason);
        Assert.Equal(1, boundary.CloseCount);
    }

    [Fact]
    public async Task ControlDisconnectTerminatesWithoutResume()
    {
        var boundary = new RecordingBoundary();
        await using var owner = CreateOwner(boundary);
        var start = await owner.StartAsync(Request());

        var ended = await owner.ControlDisconnectedAsync(
            "principal-01",
            start.Session!.SessionId);

        Assert.Equal(RuntimeHostMediaSessionState.Ended, ended.Session!.State);
        Assert.Equal(RuntimeHostMediaTerminalReason.ControlDisconnected,
            ended.Session.TerminalReason);
        Assert.Equal(1, boundary.CloseCount);
    }

    [Fact]
    public async Task LeaseExpiryTerminatesAndReleasesBoundary()
    {
        var clock = new ManualTimeProvider();
        var boundary = new RecordingBoundary();
        await using var owner = CreateOwner(boundary, clock: clock);
        var start = await owner.StartAsync(Request());
        clock.Advance(RuntimeHostMediaSessionOwner.SessionLeaseDuration);

        var result = await owner.GetStatusAsync(
            "principal-01",
            start.Session!.SessionId);

        Assert.Equal(RuntimeHostMediaOperationStatus.TimedOut, result.Status);
        Assert.Equal(RuntimeHostMediaTerminalReason.LeaseExpired,
            result.Session!.TerminalReason);
        Assert.Equal(1, boundary.CloseCount);
    }

    [Fact]
    public async Task NegotiationIdleTimeoutFaultsAndReleasesBoundary()
    {
        var clock = new ManualTimeProvider();
        var boundary = new RecordingBoundary();
        await using var owner = CreateOwner(boundary, clock: clock);
        var start = await owner.StartAsync(Request());
        clock.Advance(RuntimeHostMediaSessionOwner.NegotiationIdleTimeout);

        var result = await owner.GetStatusAsync(
            "principal-01",
            start.Session!.SessionId);

        Assert.Equal(RuntimeHostMediaOperationStatus.TimedOut, result.Status);
        Assert.Equal(RuntimeHostMediaSessionState.Faulted, result.Session!.State);
        Assert.Equal(RuntimeHostMediaTerminalReason.NegotiationTimedOut,
            result.Session.TerminalReason);
        Assert.Equal(1, boundary.CloseCount);
    }

    [Fact]
    public async Task BoundaryOpenFailureIsSanitizedAndReleased()
    {
        var boundary = new RecordingBoundary { FailOpen = true };
        await using var owner = CreateOwner(boundary);

        var result = await owner.StartAsync(Request());

        Assert.Equal(RuntimeHostMediaOperationStatus.Faulted, result.Status);
        Assert.Equal(RuntimeHostMediaTerminalReason.MediaBoundaryFailed,
            result.Session!.TerminalReason);
        Assert.Equal(1, boundary.CloseCount);
    }

    [Fact]
    public async Task HostShutdownReleasesActiveBoundaryExactlyOnce()
    {
        var boundary = new RecordingBoundary();
        await using var owner = CreateOwner(boundary);
        await owner.StartAsync(Request());

        await owner.StopForHostShutdownAsync();
        await owner.StopForHostShutdownAsync();

        Assert.Equal(1, boundary.CloseCount);
    }

    [Theory]
    [InlineData(true, RuntimeHostMediaTerminalReason.SourceLost)]
    [InlineData(false, RuntimeHostMediaTerminalReason.MediaBoundaryFailed)]
    public async Task RuntimeBoundaryTerminalSignalsFaultAndReleaseExactlyOnce(
        bool sourceLost,
        RuntimeHostMediaTerminalReason expectedReason)
    {
        var boundary = new RecordingBoundary();
        await using var owner = CreateOwner(boundary);
        var start = await owner.StartAsync(Request());

        var result = sourceLost
            ? await owner.SourceLostAsync(
                "principal-01",
                start.Session!.SessionId)
            : await owner.MediaBoundaryFailedAsync(
                "principal-01",
                start.Session!.SessionId);

        Assert.Equal(RuntimeHostMediaSessionState.Faulted, result.Session!.State);
        Assert.Equal(expectedReason, result.Session.TerminalReason);
        Assert.Equal(1, boundary.CloseCount);
    }

    private static RuntimeHostMediaSessionOwner CreateOwner(
        RecordingBoundary boundary,
        RuntimeHostMediaSourceAvailability availability =
            RuntimeHostMediaSourceAvailability.Idle,
        string? audioDeviceId = "microphone-device-local",
        TimeProvider? clock = null) =>
        new(
            new RuntimeHostMediaSourceConfiguration(
                Target(),
                "camera-device-local",
                audioDeviceId,
                availability),
            boundary,
            clock);

    private static RuntimeHostMediaStartRequest Request(
        string principal = "principal-01",
        RuntimeHostMediaSourceTarget? target = null,
        bool includeAudio = false) =>
        new(principal, target ?? Target(), includeAudio);

    private static RuntimeHostMediaSourceTarget Target() =>
        new("camera-01", "generation-01");

    private sealed class RecordingBoundary : IRuntimeHostMediaCaptureBoundary
    {
        public List<(RuntimeHostMediaSourceConfiguration Source, bool IncludeAudio)>
            Opened { get; } = [];
        public List<RuntimeHostMediaNegotiationMessage> Submitted { get; } = [];
        public int CloseCount { get; private set; }
        public bool FailOpen { get; init; }

        public ValueTask OpenAsync(
            RuntimeHostMediaSourceConfiguration source,
            bool includeAudio,
            CancellationToken cancellationToken)
        {
            Opened.Add((source, includeAudio));
            if (FailOpen)
            {
                throw new InvalidOperationException("sensitive driver detail");
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask SubmitNegotiationAsync(
            RuntimeHostMediaNegotiationMessage message,
            CancellationToken cancellationToken)
        {
            Submitted.Add(message);
            return ValueTask.CompletedTask;
        }

        public ValueTask CloseAsync(CancellationToken cancellationToken)
        {
            CloseCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset utcNow =
            new(2026, 8, 16, 8, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => utcNow;

        public void Advance(TimeSpan value) => utcNow += value;
    }
}
