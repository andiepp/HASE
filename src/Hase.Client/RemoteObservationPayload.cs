namespace Hase.Client;

/// <summary>
/// Represents the immutable normalized payload of one remote runtime-host
/// observation.
/// </summary>
public abstract record RemoteObservationPayload
{
    /// <summary>
    /// Gets the observation kind represented by this payload.
    /// </summary>
    public abstract RemoteObservationKind Kind
    {
        get;
    }
}
