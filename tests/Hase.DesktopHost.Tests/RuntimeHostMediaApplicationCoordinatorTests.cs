using System.Collections.Concurrent;
using Hase.DesktopHost.App.Media;
using Hase.Runtime.Diagnostics;
using Hase.Runtime.Media;

namespace Hase.DesktopHost.Tests;

public sealed class RuntimeHostMediaApplicationCoordinatorTests
{
    [Fact]
    public async Task ValidatedOfferAndConnectedEventReachSoleOwner()
    {
        var boundary = new FakeBoundary();
        await using var owner = new RuntimeHostMediaSessionOwner(
            new RuntimeHostMediaSourceConfiguration(
                new("camera", "generation"), "device", null,
                RuntimeHostMediaSourceAvailability.Idle),
            boundary);
        await using var coordinator =
            new RuntimeHostMediaApplicationCoordinator(boundary, owner);
        var start = await owner.StartAsync(new(
            "principal", new("camera", "generation"), false));

        boundary.Publish(new(RuntimeHostMediaWebMessageKind.Negotiation, null,
            new(1, RuntimeHostMediaNegotiationKind.Offer, "offer")));
        await WaitUntilAsync(async () =>
            (await owner.ExchangeNegotiationAsync(
                "principal", start.Session!.SessionId, 0, null))
                .DeliveredMessages.Count == 1);
        boundary.Publish(new(RuntimeHostMediaWebMessageKind.PeerConnected, null));
        await WaitUntilAsync(async () =>
            (await owner.GetStatusAsync("principal", start.Session!.SessionId))
                .Session?.State == RuntimeHostMediaSessionState.Streaming);
    }

    [Fact]
    public async Task CaptureFailure_PublishesOnlySanitizedCategory()
    {
        var boundary = new FakeBoundary();
        var sink = new RecordingDiagnosticSink();
        await using var owner = new RuntimeHostMediaSessionOwner(
            new RuntimeHostMediaSourceConfiguration(
                new("camera", "generation"), "sensitive-device", null,
                RuntimeHostMediaSourceAvailability.Idle),
            boundary);
        await using var coordinator =
            new RuntimeHostMediaApplicationCoordinator(
                boundary,
                owner,
                new RuntimeDiagnosticPublisher(sink));
        RuntimeHostMediaOperationResult start = await owner.StartAsync(new(
            "principal", new("camera", "generation"), false));

        boundary.Publish(new(
            RuntimeHostMediaWebMessageKind.CaptureFaulted,
            "device-unavailable"));

        await WaitUntilAsync(() => Task.FromResult(sink.Records.Count == 1));
        RuntimeDiagnosticRecord record = Assert.Single(sink.Records);
        Assert.Equal("MediaBoundaryFaulted", record.EventName);
        Assert.Equal(RuntimeDiagnosticSeverity.Warning, record.Severity);
        Assert.Equal(RuntimeDiagnosticOutcome.Failed, record.Outcome);
        Assert.Equal("Capture", record.Details["BoundaryKind"]);
        Assert.Equal(
            "device-unavailable",
            record.Details["FailureCategory"]);
        Assert.DoesNotContain(
            record.Details.Values,
            value => value.Contains("sensitive-device", StringComparison.Ordinal));
        await WaitUntilAsync(async () =>
            (await owner.GetStatusAsync(
                "principal",
                start.Session!.SessionId)).Session?.State ==
                    RuntimeHostMediaSessionState.Faulted);
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        for (int index = 0; index < 100; index++)
        {
            if (await condition())
            {
                return;
            }
            await Task.Delay(10);
        }
        throw new TimeoutException("The media coordinator did not converge.");
    }

    private sealed class FakeBoundary : IRuntimeHostMediaWebBoundary
    {
        public event Action<RuntimeHostMediaWebMessage>? ValidatedMessage;
        public void Publish(RuntimeHostMediaWebMessage message) =>
            ValidatedMessage?.Invoke(message);
        public ValueTask OpenAsync(RuntimeHostMediaSourceConfiguration source,
            bool includeAudio, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;
        public ValueTask SubmitNegotiationAsync(
            RuntimeHostMediaNegotiationMessage message,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public ValueTask CloseAsync(CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingDiagnosticSink : IRuntimeDiagnosticSink
    {
        public ConcurrentQueue<RuntimeDiagnosticRecord> Records { get; } = [];

        public bool IsEnabled(RuntimeDiagnosticLevel level) => true;

        public void Publish(RuntimeDiagnosticRecord record) =>
            Records.Enqueue(record);
    }
}
