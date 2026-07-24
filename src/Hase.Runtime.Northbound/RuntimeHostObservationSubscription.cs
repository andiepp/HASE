namespace Hase.Runtime.Northbound;

/// <summary>
/// Represents one active, independently buffered runtime-host observation
/// subscription.
/// </summary>
/// <remarks>
/// A subscription owns application delivery state only. Disposing it must not
/// detach, replace, shut down, or dispose a runtime endpoint attachment.
/// </remarks>
public abstract class RuntimeHostObservationSubscription
    : IAsyncDisposable
{
    /// <summary>
    /// Initializes an active observation subscription at one authoritative
    /// snapshot boundary.
    /// </summary>
    protected RuntimeHostObservationSubscription(
        PublishedRuntimeHostSnapshot initialSnapshot,
        RuntimeHostObservationSequence snapshotSequence)
    {
        InitialSnapshot =
            initialSnapshot
            ?? throw new ArgumentNullException(
                nameof(initialSnapshot));

        SnapshotSequence =
            snapshotSequence
            ?? throw new ArgumentNullException(
                nameof(snapshotSequence));
    }

    /// <summary>
    /// Gets the authoritative runtime-host state captured when the
    /// subscription was opened.
    /// </summary>
    public PublishedRuntimeHostSnapshot InitialSnapshot
    {
        get;
    }

    /// <summary>
    /// Gets the subscription-local sequence represented by the initial
    /// snapshot.
    /// </summary>
    public RuntimeHostObservationSequence SnapshotSequence
    {
        get;
    }

    /// <summary>
    /// Reads all observations later than <see cref="SnapshotSequence"/> until
    /// the subscription ends, enumeration is cancelled, or an observation gap
    /// terminates the stream.
    /// </summary>
    public abstract IAsyncEnumerable<RuntimeHostObservation> ReadAllAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops later delivery and releases resources owned by this subscription.
    /// Implementations must make repeated disposal safe.
    /// </summary>
    public abstract ValueTask DisposeAsync();
}