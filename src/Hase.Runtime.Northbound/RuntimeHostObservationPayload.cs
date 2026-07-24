namespace Hase.Runtime.Northbound;

/// <summary>
/// Represents the immutable normalized payload of one runtime-host
/// observation.
/// </summary>
public abstract record RuntimeHostObservationPayload
{
    /// <summary>
    /// Gets the observation kind represented by this payload.
    /// </summary>
    public abstract RuntimeHostObservationKind Kind
    {
        get;
    }
}