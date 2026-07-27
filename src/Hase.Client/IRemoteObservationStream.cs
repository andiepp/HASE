namespace Hase.Client;

/// <summary>
/// Provides one transport-neutral remote runtime-host observation stream.
/// </summary>
/// <remarks>
/// Implementations adapt a concrete transport such as gRPC. One instance
/// represents one subscription and must publish exactly one initial snapshot
/// before its later observations are consumed.
/// </remarks>
public interface IRemoteObservationStream
{
    /// <summary>
    /// Reads the mandatory authoritative initial snapshot.
    /// </summary>
    ValueTask<RemoteObservationInitialSnapshot> ReadInitialSnapshotAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the later subscription-local observations until the remote stream
    /// completes, fails, or is cancelled.
    /// </summary>
    IAsyncEnumerable<RemoteRuntimeHostObservation> ReadObservationsAsync(
        CancellationToken cancellationToken = default);
}
