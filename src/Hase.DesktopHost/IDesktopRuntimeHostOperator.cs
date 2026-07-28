using Hase.Runtime.Northbound;

namespace Hase.DesktopHost;

/// <summary>
/// Executes explicit generation-scoped operator mutations through the
/// normalized runtime-host application boundary.
/// </summary>
public interface IDesktopRuntimeHostOperator
{
    /// <summary>
    /// Writes one Property exactly once and returns its normalized
    /// endpoint-confirmed result.
    /// </summary>
    Task<RuntimeHostPropertyOperationResult> WritePropertyAsync(
        RuntimeHostPropertyTarget target,
        object? requestedValue,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes one Command exactly once and returns its normalized result.
    /// </summary>
    Task<RuntimeHostCommandOperationResult> ExecuteCommandAsync(
        RuntimeHostCommandTarget target,
        object? argument,
        CancellationToken cancellationToken = default);
}
