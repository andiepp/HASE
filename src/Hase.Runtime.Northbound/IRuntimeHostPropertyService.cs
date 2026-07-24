namespace Hase.Runtime.Northbound;

/// <summary>
/// Provides generation-scoped cached and authoritative Property operations for
/// applications using one runtime host.
/// </summary>
public interface IRuntimeHostPropertyService
{
    /// <summary>
    /// Captures one cached Property snapshot without endpoint communication.
    /// </summary>
    RuntimeHostCachedPropertyResult GetCached(
        RuntimeHostPropertyTarget target);

    /// <summary>
    /// Reads one Property authoritatively from its current attachment.
    /// </summary>
    Task<RuntimeHostPropertyOperationResult> ReadAsync(
        RuntimeHostPropertyTarget target,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes one Property and returns its endpoint-confirmed value.
    /// </summary>
    Task<RuntimeHostPropertyOperationResult> WriteAsync(
        RuntimeHostPropertyTarget target,
        object? requestedValue,
        CancellationToken cancellationToken = default);
}