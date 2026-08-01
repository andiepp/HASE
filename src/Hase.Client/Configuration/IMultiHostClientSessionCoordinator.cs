namespace Hase.Client.Configuration;

/// <summary>
/// Coordinates independently managed runtime-host profile sessions.
/// </summary>
public interface IMultiHostClientSessionCoordinator : IAsyncDisposable
{
    event EventHandler? SnapshotChanged;

    MultiHostClientSessionSnapshot Snapshot { get; }

    Task ConnectAsync(
        RuntimeHostProfileId profileId,
        CancellationToken cancellationToken = default);

    Task DisconnectAsync(
        RuntimeHostProfileId profileId);
}
