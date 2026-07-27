namespace Hase.Client;

/// <summary>
/// Executes explicit authoritative Property writes through one connected
/// runtime-host client session.
/// </summary>
public interface IRuntimeHostPropertyWriter
{
    /// <summary>
    /// Writes one exact generation-scoped Property without automatic retry.
    /// </summary>
    Task<RemotePropertyOperationResult> WritePropertyAsync(
        RemotePropertyTarget target,
        RemoteValue requestedValue,
        CancellationToken cancellationToken = default);
}
