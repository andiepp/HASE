namespace Hase.Client;

/// <summary>
/// Executes explicit Commands through one connected runtime-host session.
/// </summary>
public interface IRuntimeHostCommandExecutor
{
    /// <summary>
    /// Executes one Command exactly once without automatic retry.
    /// </summary>
    Task<RemoteCommandOperationResult> ExecuteCommandAsync(
        RemoteCommandExecutionRequest request,
        CancellationToken cancellationToken = default);
}
