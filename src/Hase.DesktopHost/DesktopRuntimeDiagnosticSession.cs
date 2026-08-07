using Hase.Runtime.Diagnostics;
using Hase.Runtime.Northbound;

namespace Hase.DesktopHost;

/// <summary>
/// Owns one bounded process-local Desktop Runtime Host diagnostic session.
/// </summary>
public sealed class DesktopRuntimeDiagnosticSession
    : IDesktopRuntimeDiagnosticSource,
      IAsyncDisposable
{
    public const int DefaultCapacity =
        2000;

    private readonly BoundedRuntimeDiagnosticCollector collector;
    private readonly ForwardingDiagnosticSink forwardingSink;
    private readonly object gate = new();
    private RuntimeHostDiagnosticProjectionService? projectionService;
    private bool isDisposed;

    public DesktopRuntimeDiagnosticSession(
        RuntimeDiagnosticLevel maximumLevel =
            RuntimeDiagnosticLevel.Operational,
        int capacity = DefaultCapacity)
    {
        collector =
            new BoundedRuntimeDiagnosticCollector(
                capacity,
                maximumLevel);

        forwardingSink =
            new ForwardingDiagnosticSink(
                collector);

        Publisher =
            new RuntimeDiagnosticPublisher(
                forwardingSink);
    }

    public RuntimeDiagnosticLevel MaximumLevel =>
        collector.MaximumLevel;

    public RuntimeDiagnosticPublisher Publisher
    {
        get;
    }

    /// <summary>
    /// Gets the explicitly attached diagnostic projection service, or null
    /// while this session remains local-only.
    /// </summary>
    public RuntimeHostDiagnosticProjectionService? ProjectionService
    {
        get
        {
            lock (gate)
            {
                return projectionService;
            }
        }
    }

    /// <summary>
    /// Atomically attaches one identity-aware live diagnostic projection while
    /// preserving this session's existing publisher and local retention.
    /// </summary>
    public RuntimeHostDiagnosticProjectionService AttachProjection(
        RuntimeHostId runtimeHostId,
        RuntimeHostDiagnosticProjectionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(runtimeHostId);
        ArgumentNullException.ThrowIfNull(policy);

        lock (gate)
        {
            ObjectDisposedException.ThrowIf(isDisposed, this);

            if (projectionService is not null)
            {
                throw new InvalidOperationException(
                    "A diagnostic projection is already attached.");
            }

            var service = new RuntimeHostDiagnosticProjectionService(
                runtimeHostId,
                collector,
                MaximumLevel,
                policy);
            projectionService = service;
            forwardingSink.Redirect(service);
            return service;
        }
    }

    public IReadOnlyList<RuntimeDiagnosticRecord> CaptureDiagnostics()
    {
        return collector.GetSnapshot();
    }

    public void ClearDiagnostics()
    {
        collector.Clear();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        RuntimeHostDiagnosticProjectionService? service;

        lock (gate)
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            service = projectionService;
            forwardingSink.Redirect(collector);
        }

        if (service is not null)
        {
            await service.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed class ForwardingDiagnosticSink : IRuntimeDiagnosticSink
    {
        private IRuntimeDiagnosticSink current;

        public ForwardingDiagnosticSink(IRuntimeDiagnosticSink initial)
        {
            current = initial
                ?? throw new ArgumentNullException(nameof(initial));
        }

        public bool IsEnabled(RuntimeDiagnosticLevel level)
        {
            return Volatile.Read(ref current).IsEnabled(level);
        }

        public void Publish(RuntimeDiagnosticRecord record)
        {
            Volatile.Read(ref current).Publish(record);
        }

        public void Redirect(IRuntimeDiagnosticSink sink)
        {
            ArgumentNullException.ThrowIfNull(sink);
            Volatile.Write(ref current, sink);
        }
    }
}
