namespace Hase.Client;

/// <summary>
/// Represents the mandatory authoritative initial boundary of one remote
/// observation subscription.
/// </summary>
public sealed record RemoteObservationInitialSnapshot
{
    /// <summary>
    /// Initializes one remote observation initial snapshot.
    /// </summary>
    public RemoteObservationInitialSnapshot(
        RemoteRuntimeHostSnapshot snapshot,
        RemoteObservationSequence snapshotSequence)
    {
        Snapshot =
            snapshot
            ?? throw new ArgumentNullException(
                nameof(snapshot));

        SnapshotSequence =
            snapshotSequence
            ?? throw new ArgumentNullException(
                nameof(snapshotSequence));
    }

    /// <summary>
    /// Gets the authoritative remote runtime-host state captured when the
    /// subscription opened.
    /// </summary>
    public RemoteRuntimeHostSnapshot Snapshot
    {
        get;
    }

    /// <summary>
    /// Gets the subscription-local sequence represented by the initial
    /// snapshot.
    /// </summary>
    public RemoteObservationSequence SnapshotSequence
    {
        get;
    }
}
