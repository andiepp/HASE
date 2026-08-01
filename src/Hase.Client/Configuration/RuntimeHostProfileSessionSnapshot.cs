namespace Hase.Client.Configuration;

/// <summary>
/// Represents one immutable client-local view of an independently managed
/// runtime-host profile session.
/// </summary>
public sealed record RuntimeHostProfileSessionSnapshot
{
    public RuntimeHostProfileSessionSnapshot(
        RuntimeHostProfile profile,
        RuntimeHostClientSessionStatus status,
        DateTimeOffset changedAtUtc,
        RemoteObservationState? currentState = null,
        RuntimeHostClientFailureSnapshot? failure = null)
    {
        Profile =
            profile
            ?? throw new ArgumentNullException(nameof(profile));
        Status =
            status
            ?? throw new ArgumentNullException(nameof(status));

        if (changedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The session state-change timestamp must use UTC.",
                nameof(changedAtUtc));
        }

        if (status.State == RuntimeHostClientSessionState.Connected)
        {
            if (currentState is null || !currentState.IsInitialized)
            {
                throw new ArgumentException(
                    "A connected profile session requires initialized remote state.",
                    nameof(currentState));
            }

            if (currentState.Snapshot!.RuntimeHostId
                    != profile.ExpectedRuntimeHostId
                || currentState.Snapshot.RuntimeHostId
                    != status.RuntimeHostId
                || currentState.Snapshot.ApiVersion
                    != status.ApiVersion)
            {
                throw new ArgumentException(
                    "The connected remote state must match the expected host and session status.",
                    nameof(currentState));
            }
        }

        if (currentState is not null
            && currentState.IsInitialized
            && currentState.Snapshot!.RuntimeHostId
                != profile.ExpectedRuntimeHostId)
        {
            throw new ArgumentException(
                "Retained remote state must belong to the profile's expected runtime host.",
                nameof(currentState));
        }

        if (status.State == RuntimeHostClientSessionState.Faulted
            && failure is null)
        {
            throw new ArgumentException(
                "A faulted profile session requires a normalized failure.",
                nameof(failure));
        }

        if (status.State != RuntimeHostClientSessionState.Faulted
            && failure is not null)
        {
            throw new ArgumentException(
                "Only a faulted profile session may expose a normalized failure.",
                nameof(failure));
        }

        ChangedAtUtc = changedAtUtc;
        CurrentState = currentState;
        Failure = failure;
    }

    public RuntimeHostProfile Profile { get; }

    public RuntimeHostProfileId ProfileId => Profile.ProfileId;

    public RuntimeHostClientSessionStatus Status { get; }

    public DateTimeOffset ChangedAtUtc { get; }

    public RemoteObservationState? CurrentState { get; }

    public RuntimeHostClientFailureSnapshot? Failure { get; }
}
