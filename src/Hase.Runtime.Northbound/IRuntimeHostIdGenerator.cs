namespace Hase.Runtime.Northbound;

/// <summary>
/// Generates runtime-host identities for first-use identity resolution.
/// </summary>
public interface IRuntimeHostIdGenerator
{
    /// <summary>
    /// Generates a runtime-host identity candidate.
    /// </summary>
    RuntimeHostId Generate();
}