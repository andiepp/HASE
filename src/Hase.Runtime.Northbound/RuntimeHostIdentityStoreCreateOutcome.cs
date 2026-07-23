namespace Hase.Runtime.Northbound;

/// <summary>
/// Describes the outcome of atomically creating persisted runtime-host
/// identity when it is missing.
/// </summary>
public enum RuntimeHostIdentityStoreCreateOutcome
{
    /// <summary>
    /// The candidate identity was created.
    /// </summary>
    Created = 0,

    /// <summary>
    /// An identity already existed and was returned instead.
    /// </summary>
    Existing = 1,
}