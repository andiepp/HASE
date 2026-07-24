namespace Hase.Runtime.Northbound;

/// <summary>
/// Provides generation-scoped Command execution for applications using one
/// runtime host.
/// </summary>
public interface IRuntimeHostCommandService
{
    /// <summary>
    /// Executes one Command through its current attachment.
    /// </summary>
    Task<RuntimeHostCommandOperationResult> ExecuteAsync(
        RuntimeHostCommandTarget target,
        object? argument,
        CancellationToken cancellationToken = default);
}