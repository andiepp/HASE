namespace Hase.Runtime.Northbound;

/// <summary>
/// Provides persistent runtime-host identity storage.
/// </summary>
public interface IRuntimeHostIdentityStore
{
    /// <summary>
    /// Reads the persisted runtime-host identity, when one exists.
    /// </summary>
    Task<RuntimeHostId?> ReadAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically creates the candidate identity when no persisted identity
    /// exists and returns the authoritative persisted identity.
    /// </summary>
    Task<RuntimeHostIdentityStoreCreateResult> CreateIfMissingAsync(
        RuntimeHostId candidate,
        CancellationToken cancellationToken = default);
}