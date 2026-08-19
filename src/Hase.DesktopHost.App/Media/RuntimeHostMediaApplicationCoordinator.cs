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
    private readonly IRuntimeHostMediaInventoryWebBoundary? inventoryBoundary;
    private readonly RuntimeHostMediaInventoryReconciler? inventoryReconciler;
    private readonly RuntimeDiagnosticPublisher diagnostics;
    private readonly Channel<RuntimeHostMediaWebMessage> messages =
        Channel.CreateBounded<RuntimeHostMediaWebMessage>(64);
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task pump;
    private readonly Channel<IReadOnlyList<RuntimeHostMediaDeviceObservation>>
        inventoryUpdates = Channel.CreateBounded<
            IReadOnlyList<RuntimeHostMediaDeviceObservation>>(
                new BoundedChannelOptions(1)
                {
                    SingleReader = true,
                    SingleWriter = false,
                    FullMode = BoundedChannelFullMode.DropOldest
                });
    private readonly Task inventoryPump;

    public RuntimeHostMediaApplicationCoordinator(
        IRuntimeHostMediaWebBoundary boundary,
        RuntimeHostMediaSessionOwner owner,
        RuntimeDiagnosticPublisher? diagnostics = null)
        : this(
            boundary,
            owner,
            inventoryBoundary: null,
            inventoryReconciler: null,
            diagnostics: diagnostics)
    {
    }

    public RuntimeHostMediaApplicationCoordinator(
        IRuntimeHostMediaWebBoundary boundary,
        RuntimeHostMediaSessionOwner owner,
        IRuntimeHostMediaInventoryWebBoundary? inventoryBoundary,
        RuntimeHostMediaInventoryReconciler? inventoryReconciler,
        RuntimeDiagnosticPublisher? diagnostics = null)
    {
        this.boundary = boundary ?? throw new ArgumentNullException(nameof(boundary));
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.diagnostics = diagnostics ?? new RuntimeDiagnosticPublisher();
        if ((inventoryBoundary is null) != (inventoryReconciler is null))
        {
            throw new ArgumentException(
                "The inventory boundary and reconciler must be composed together.");
        }
        this.inventoryBoundary = inventoryBoundary;
        this.inventoryReconciler = inventoryReconciler;
        boundary.ValidatedMessage += OnValidatedMessage;
        if (inventoryBoundary is not null)
        {
            inventoryBoundary.InventoryChanged += OnInventoryChanged;
        }
        pump = PumpAsync(cancellation.Token);
        inventoryPump = PumpInventoryAsync(cancellation.Token);
    }

    public async ValueTask InitializeInventoryAsync(
        CancellationToken cancellationToken = default)
    {
        if (inventoryBoundary is not null)
        {
            await inventoryBoundary.InitializeAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        boundary.ValidatedMessage -= OnValidatedMessage;
        if (inventoryBoundary is not null)
        {
            inventoryBoundary.InventoryChanged -= OnInventoryChanged;
        }
        messages.Writer.TryComplete();
        inventoryUpdates.Writer.TryComplete();
        cancellation.Cancel();
        try
        {
            await Task.WhenAll(pump, inventoryPump).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (cancellation.IsCancellationRequested)
        {
        }
        await owner.StopForHostShutdownAsync().ConfigureAwait(false);
        await owner.DisposeAsync().ConfigureAwait(false);
        await boundary.DisposeAsync().ConfigureAwait(false);
        if (inventoryBoundary is not null)
        {
            await inventoryBoundary.DisposeAsync().ConfigureAwait(false);
        }
        cancellation.Dispose();
    }

    private void OnInventoryChanged(
        IReadOnlyList<RuntimeHostMediaDeviceObservation> observations)
    {
        inventoryUpdates.Writer.TryWrite(observations);
    }

    private async Task PumpInventoryAsync(CancellationToken cancellationToken)
    {
        await foreach (IReadOnlyList<RuntimeHostMediaDeviceObservation>
            observations in inventoryUpdates.Reader
                .ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                IReadOnlyList<RuntimeHostMediaSourceConfiguration> sources =
                    inventoryReconciler?.Reconcile(observations) ?? [];
                await owner.ReplaceSourcesAsync(sources, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (ArgumentException)
            {
                diagnostics.Publish(
                    RuntimeDiagnosticLevel.Operational,
                    () => new RuntimeDiagnosticEvent(
                        RuntimeDiagnosticLevel.Operational,
                        RuntimeDiagnosticCategory.RuntimeConnection,
                        "MediaInventoryRejected",
                        RuntimeDiagnosticSeverity.Warning,
                        outcome: RuntimeDiagnosticOutcome.Failed,
                        details: new Dictionary<string, string>(
                            StringComparer.Ordinal)
                        {
                            ["FailureCategory"] = "InvalidSnapshot"
                        }));
            }
        }
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
