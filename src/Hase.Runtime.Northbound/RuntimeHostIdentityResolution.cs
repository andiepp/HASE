namespace Hase.Runtime.Northbound;

/// <summary>
/// Represents the successful resolution of authoritative runtime-host
/// identity.
/// </summary>
public sealed record RuntimeHostIdentityResolution
{
    /// <summary>
    /// Initializes an identity-resolution result.
    /// </summary>
    public RuntimeHostIdentityResolution(
        RuntimeHostId runtimeHostId,
        RuntimeHostIdentityOrigin origin)
    {
        RuntimeHostId =
            runtimeHostId
            ?? throw new ArgumentNullException(
                nameof(runtimeHostId));

        if (!Enum.IsDefined(
                origin))
        {
            throw new ArgumentOutOfRangeException(
                nameof(origin),
                origin,
                "The runtime-host identity origin is not defined.");
        }

        Origin =
            origin;
    }

    /// <summary>
    /// Gets the authoritative resolved runtime-host identity.
    /// </summary>
    public RuntimeHostId RuntimeHostId
    {
        get;
    }

    /// <summary>
    /// Gets the diagnostic origin of the resolved identity.
    /// </summary>
    public RuntimeHostIdentityOrigin Origin
    {
        get;
    }
}