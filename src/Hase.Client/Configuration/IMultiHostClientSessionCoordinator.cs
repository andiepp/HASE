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

    Task<RemotePropertyOperationResult> ReadPropertyAsync(RemoteRuntimeHostPropertyTarget target, CancellationToken cancellationToken = default);
    Task<RemotePropertyOperationResult> WritePropertyAsync(RemoteRuntimeHostPropertyTarget target, RemoteValue requestedValue, CancellationToken cancellationToken = default);
    Task<RemoteCommandOperationResult> ExecuteCommandAsync(RemoteRuntimeHostCommandExecutionRequest request, CancellationToken cancellationToken = default);
}
