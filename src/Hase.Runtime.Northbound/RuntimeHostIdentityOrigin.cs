namespace Hase.Runtime.Northbound;

/// <summary>
/// Describes how the authoritative runtime-host identity was resolved.
/// </summary>
public enum RuntimeHostIdentityOrigin
{
    /// <summary>
    /// The identity was supplied explicitly by host configuration.
    /// </summary>
    ExplicitConfiguration,

    /// <summary>
    /// The identity already existed in the persistent identity store.
    /// </summary>
    Persisted,

    /// <summary>
    /// The identity was generated and persisted by this resolution attempt.
    /// </summary>
    GeneratedAndPersisted
}