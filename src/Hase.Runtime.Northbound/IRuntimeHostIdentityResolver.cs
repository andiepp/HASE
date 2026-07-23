namespace Hase.Runtime.Northbound;

/// <summary>
/// Resolves the authoritative runtime-host identity.
/// </summary>
public interface IRuntimeHostIdentityResolver
{
    /// <summary>
    /// Resolves the authoritative runtime-host identity using optional
    /// explicit configuration and the configured identity store.
    /// </summary>
    Task<RuntimeHostIdentityResolution> ResolveAsync(
        RuntimeHostId? configuredRuntimeHostId = null,
        CancellationToken cancellationToken = default);
}