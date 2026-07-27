namespace Hase.Client;

/// <summary>
/// Executes explicit authoritative Property reads through one connected
/// runtime-host client session.
/// </summary>
public interface IRuntimeHostPropertyReader
{
    /// <summary>
    /// Reads one exact generation-scoped Property without automatic retry.
    /// </summary>
    Task<RemotePropertyOperationResult> ReadPropertyAsync(
        RemotePropertyTarget target,
        CancellationToken cancellationToken = default);
}
