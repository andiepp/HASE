using Hase.Runtime.Diagnostics;

namespace Hase.Runtime.Northbound;

/// <summary>
/// Decorates one local diagnostic sink and independently fans explicitly
/// permitted records out to bounded live-only projection subscriptions.
/// </summary>
public sealed class RuntimeHostDiagnosticProjectionService
    : IRuntimeDiagnosticSink,
      IAsyncDisposable
{
    private readonly IRuntimeDiagnosticSink downstream;
    private readonly RuntimeHostDiagnosticProjector projector;
    private readonly object gate = new();
    private readonly List<BufferedRuntimeHostDiagnosticProjectionSubscription>
        subscriptions = [];
    private bool isDisposed;

    public RuntimeHostDiagnosticProjectionService(
        RuntimeHostId runtimeHostId,
        IRuntimeDiagnosticSink downstream,
        RuntimeDiagnosticLevel hostMaximumLevel,
        RuntimeHostDiagnosticProjectionPolicy? policy = null)
    {
        this.downstream = downstream
            ?? throw new ArgumentNullException(nameof(downstream));
        projector = new RuntimeHostDiagnosticProjector(
            runtimeHostId,
            hostMaximumLevel,
            policy);
    }

    public Task<RuntimeHostDiagnosticProjectionSubscription>
        OpenSubscriptionAsync(
            RuntimeHostDiagnosticProjectionSubscriptionOptions options,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            ObjectDisposedException.ThrowIf(isDisposed, this);

            var subscription =
                new BufferedRuntimeHostDiagnosticProjectionSubscription(
                    options.BufferCapacity,
                    RemoveSubscription);
            subscriptions.Add(subscription);
            return Task.FromResult<RuntimeHostDiagnosticProjectionSubscription>(
                subscription);
        }
    }

    public bool IsEnabled(RuntimeDiagnosticLevel level)
    {
        bool downstreamEnabled;
        try
        {
            downstreamEnabled = downstream.IsEnabled(level);
        }
        catch
        {
            downstreamEnabled = false;
        }

        return downstreamEnabled || projector.IsEnabled(level);
    }

    public void Publish(RuntimeDiagnosticRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        try
        {
            downstream.Publish(record);
        }
        catch
        {
            // Local diagnostic sink failure must not affect remote projection.
        }

        if (!projector.TryProject(record, out RuntimeHostProjectedDiagnosticRecord? projected))
        {
            return;
        }

        lock (gate)
        {
            if (isDisposed)
            {
                return;
            }

            foreach (
                BufferedRuntimeHostDiagnosticProjectionSubscription subscription
                in subscriptions.ToArray())
            {
                if (!subscription.TryEnqueue(projected))
                {
                    subscriptions.Remove(subscription);
                }
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        BufferedRuntimeHostDiagnosticProjectionSubscription[] active;

        lock (gate)
        {
            if (isDisposed)
            {
                return ValueTask.CompletedTask;
            }

            isDisposed = true;
            active = subscriptions.ToArray();
            subscriptions.Clear();
        }

        foreach (BufferedRuntimeHostDiagnosticProjectionSubscription subscription
            in active)
        {
            subscription.End();
        }

        return ValueTask.CompletedTask;
    }

    private void RemoveSubscription(
        BufferedRuntimeHostDiagnosticProjectionSubscription subscription)
    {
        lock (gate)
        {
            subscriptions.Remove(subscription);
        }
    }
}
