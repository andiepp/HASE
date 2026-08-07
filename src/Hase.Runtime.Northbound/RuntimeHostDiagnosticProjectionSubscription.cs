namespace Hase.Runtime.Northbound;

/// <summary>
/// Represents one independently buffered live-only diagnostic projection
/// subscription.
/// </summary>
public abstract class RuntimeHostDiagnosticProjectionSubscription
    : IAsyncDisposable
{
    public abstract IAsyncEnumerable<RuntimeHostProjectedDiagnosticObservation>
        ReadAllAsync(CancellationToken cancellationToken = default);

    public abstract ValueTask DisposeAsync();
}
