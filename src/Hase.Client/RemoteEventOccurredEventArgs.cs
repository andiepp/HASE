namespace Hase.Client;

/// <summary>
/// Carries one validated transient Event observation.
/// </summary>
public sealed class RemoteEventOccurredEventArgs
    : EventArgs
{
    public RemoteEventOccurredEventArgs(
        RemoteRuntimeHostObservation observation)
    {
        Observation =
            observation
            ?? throw new ArgumentNullException(
                nameof(observation));

        if (observation.Payload
            is not RemoteEventOccurredObservationPayload)
        {
            throw new ArgumentException(
                "An Event-occurrence payload is required.",
                nameof(observation));
        }
    }

    public RemoteRuntimeHostObservation Observation
    {
        get;
    }
}
