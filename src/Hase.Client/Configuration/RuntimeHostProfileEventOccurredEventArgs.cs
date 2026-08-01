namespace Hase.Client.Configuration;

public sealed class RuntimeHostProfileEventOccurredEventArgs : EventArgs
{
    public RuntimeHostProfileEventOccurredEventArgs(
        RuntimeHostProfileId profileId,
        RemoteRuntimeHostId runtimeHostId,
        RemoteRuntimeHostObservation observation)
    {
        ProfileId = profileId ?? throw new ArgumentNullException(nameof(profileId));
        RuntimeHostId = runtimeHostId ?? throw new ArgumentNullException(nameof(runtimeHostId));
        Observation = observation ?? throw new ArgumentNullException(nameof(observation));
        if (observation.Payload is not RemoteEventOccurredObservationPayload)
            throw new ArgumentException("An Event-occurrence observation is required.", nameof(observation));
    }
    public RuntimeHostProfileId ProfileId { get; }
    public RemoteRuntimeHostId RuntimeHostId { get; }
    public RemoteRuntimeHostObservation Observation { get; }
}
