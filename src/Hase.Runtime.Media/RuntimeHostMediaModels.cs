namespace Hase.Runtime.Media;

public enum RuntimeHostMediaSourceAvailability
{
    Unavailable,
    Idle,
    Busy,
    Faulted
}

public enum RuntimeHostMediaSessionState
{
    Starting,
    Negotiating,
    Streaming,
    Stopping,
    Ended,
    Faulted
}

public enum RuntimeHostMediaTerminalReason
{
    None,
    ClientStopped,
    ControlDisconnected,
    LeaseExpired,
    NegotiationTimedOut,
    SourceLost,
    MediaBoundaryFailed,
    HostStopping,
    ProtocolRejected
}

public enum RuntimeHostMediaOperationStatus
{
    Success,
    InvalidRequest,
    SourceNotCurrent,
    SourceUnavailable,
    SessionBusy,
    AudioNotSupported,
    SessionNotFound,
    SessionNotOwned,
    InvalidState,
    LimitExceeded,
    TimedOut,
    Faulted
}

public enum RuntimeHostMediaNegotiationKind
{
    Offer,
    Answer,
    IceCandidate,
    IceComplete
}

public sealed record RuntimeHostMediaSourceTarget(
    string MediaSourceId,
    string MediaSourceGeneration);

public sealed record RuntimeHostMediaSourceConfiguration(
    RuntimeHostMediaSourceTarget Target,
    string VideoDeviceId,
    string? AudioDeviceId,
    RuntimeHostMediaSourceAvailability Availability,
    string DisplayName)
{
    public RuntimeHostMediaSourceConfiguration(
        RuntimeHostMediaSourceTarget target,
        string videoDeviceId,
        string? audioDeviceId,
        RuntimeHostMediaSourceAvailability availability)
        : this(
            target,
            videoDeviceId,
            audioDeviceId,
            availability,
            target.MediaSourceId)
    {
    }

    public bool SupportsAudio => !string.IsNullOrWhiteSpace(AudioDeviceId);
}

public sealed record RuntimeHostMediaDeviceObservation(string VideoDeviceId);

public sealed record RuntimeHostMediaCapabilitySnapshot(
    ulong Revision,
    IReadOnlyList<RuntimeHostMediaSourceConfiguration> Sources);

public sealed record RuntimeHostMediaStartRequest(
    string PrincipalId,
    RuntimeHostMediaSourceTarget Target,
    bool IncludeAudio);

public sealed record RuntimeHostMediaNegotiationMessage(
    uint Sequence,
    RuntimeHostMediaNegotiationKind Kind,
    string SensitivePayload);

public sealed record RuntimeHostMediaSessionSnapshot(
    string SessionId,
    RuntimeHostMediaSourceTarget Target,
    bool AudioRequested,
    RuntimeHostMediaSessionState State,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset LastTransitionAtUtc,
    DateTimeOffset LeaseExpiresAtUtc,
    RuntimeHostMediaTerminalReason TerminalReason);

public sealed record RuntimeHostMediaOperationResult(
    RuntimeHostMediaOperationStatus Status,
    RuntimeHostMediaSessionSnapshot? Session)
{
    public static RuntimeHostMediaOperationResult Rejected(
        RuntimeHostMediaOperationStatus status) => new(status, null);
}

public sealed record RuntimeHostMediaNegotiationExchangeResult(
    RuntimeHostMediaOperationStatus Status,
    RuntimeHostMediaSessionSnapshot? Session,
    uint AcceptedSubmissionSequence,
    IReadOnlyList<RuntimeHostMediaNegotiationMessage> DeliveredMessages,
    bool HasMore)
{
    public static RuntimeHostMediaNegotiationExchangeResult Rejected(
        RuntimeHostMediaOperationStatus status,
        RuntimeHostMediaSessionSnapshot? session = null) =>
        new(status, session, 0, [], false);
}
