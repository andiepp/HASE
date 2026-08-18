using System.Threading.Channels;
using Hase.Runtime.Diagnostics;
using Hase.Runtime.Media;

namespace Hase.DesktopHost.App.Media;

/// <summary>
/// Serializes validated WebView2 output into the single Runtime Host media
/// session owner without logging sensitive negotiation payloads.
/// </summary>
public sealed class RuntimeHostMediaApplicationCoordinator : IAsyncDisposable
{
    private readonly IRuntimeHostMediaWebBoundary boundary;
    private readonly RuntimeHostMediaSessionOwner owner;
    private readonly RuntimeDiagnosticPublisher diagnostics;
    private readonly Channel<RuntimeHostMediaWebMessage> messages =
        Channel.CreateBounded<RuntimeHostMediaWebMessage>(64);
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task pump;

    public RuntimeHostMediaApplicationCoordinator(
        IRuntimeHostMediaWebBoundary boundary,
        RuntimeHostMediaSessionOwner owner,
        RuntimeDiagnosticPublisher? diagnostics = null)
    {
        this.boundary = boundary ?? throw new ArgumentNullException(nameof(boundary));
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.diagnostics = diagnostics ?? new RuntimeDiagnosticPublisher();
        boundary.ValidatedMessage += OnValidatedMessage;
        pump = PumpAsync(cancellation.Token);
    }

    public async ValueTask DisposeAsync()
    {
        boundary.ValidatedMessage -= OnValidatedMessage;
        messages.Writer.TryComplete();
        cancellation.Cancel();
        try
        {
            await pump.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (cancellation.IsCancellationRequested)
        {
        }
        await owner.StopForHostShutdownAsync().ConfigureAwait(false);
        await owner.DisposeAsync().ConfigureAwait(false);
        await boundary.DisposeAsync().ConfigureAwait(false);
        cancellation.Dispose();
    }

    private void OnValidatedMessage(RuntimeHostMediaWebMessage message)
    {
        if (!messages.Writer.TryWrite(message))
        {
            _ = owner.FailActiveBoundaryAsync();
        }
    }

    private async Task PumpAsync(CancellationToken cancellationToken)
    {
        await foreach (RuntimeHostMediaWebMessage message in
            messages.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            switch (message.Kind)
            {
                case RuntimeHostMediaWebMessageKind.Negotiation
                    when message.NegotiationMessage is not null:
                    await owner.PublishActiveNegotiationAsync(
                        message.NegotiationMessage, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                case RuntimeHostMediaWebMessageKind.PeerConnected:
                    await owner.MarkActiveStreamingAsync(cancellationToken)
                        .ConfigureAwait(false);
                    break;
                case RuntimeHostMediaWebMessageKind.CaptureFaulted:
                case RuntimeHostMediaWebMessageKind.PeerFaulted:
                    PublishBoundaryFailure(message);
                    await owner.FailActiveBoundaryAsync(cancellationToken)
                        .ConfigureAwait(false);
                    break;
            }
        }
    }

    private void PublishBoundaryFailure(RuntimeHostMediaWebMessage message)
    {
        string boundaryKind = message.Kind ==
            RuntimeHostMediaWebMessageKind.CaptureFaulted
                ? "Capture"
                : "Peer";
        string failureCategory = message.FailureCode ?? "Unspecified";

        diagnostics.Publish(
            RuntimeDiagnosticLevel.Operational,
            () => new RuntimeDiagnosticEvent(
                RuntimeDiagnosticLevel.Operational,
                RuntimeDiagnosticCategory.RuntimeConnection,
                "MediaBoundaryFaulted",
                RuntimeDiagnosticSeverity.Warning,
                outcome: RuntimeDiagnosticOutcome.Failed,
                details: new Dictionary<string, string>(
                    StringComparer.Ordinal)
                {
                    ["BoundaryKind"] = boundaryKind,
                    ["FailureCategory"] = failureCategory
                }));
    }
}
