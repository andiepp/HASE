namespace Hase.Client.Media;

public enum RemoteMediaSourceAvailability
{
    Unavailable,
    Idle,
    Busy,
    Faulted
}

public enum RemoteMediaSessionState
{
    Starting,
    Negotiating,
    Streaming,
    Stopping,
    Ended,
    Faulted
}

public enum RemoteMediaTerminalReason
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

public enum RemoteMediaNegotiationKind
{
    Offer,
    Answer,
    IceCandidate,
    IceComplete
}

public sealed record RemoteMediaSourceTarget(
    string MediaSourceId,
    string MediaSourceGeneration);

public sealed record RemoteMediaSourceCapability(
    RemoteMediaSourceTarget Target,
    string DisplayName,
    RemoteMediaSourceAvailability Availability,
    bool SupportsVideo,
    bool SupportsAudio);

public sealed record RemoteMediaCapabilitySnapshot(
    ulong Revision,
    IReadOnlyList<RemoteMediaSourceCapability> Sources);

public sealed record RemoteMediaSessionSnapshot(
    string SessionId,
    RemoteMediaSourceTarget Target,
    bool AudioRequested,
    RemoteMediaSessionState State,
    RemoteMediaTerminalReason TerminalReason = RemoteMediaTerminalReason.None,
    DateTimeOffset? StartedAtUtc = null,
    DateTimeOffset? LastTransitionAtUtc = null,
    DateTimeOffset? LeaseExpiresAtUtc = null);

public sealed record RemoteMediaNegotiationMessage(
    uint Sequence,
    RemoteMediaNegotiationKind Kind,
    string SensitivePayload);

public sealed record RemoteMediaStartResult(
    bool Succeeded,
    RemoteMediaSessionSnapshot? Session,
    string? FailureCode);

public sealed record RemoteMediaStopResult(
    bool Succeeded,
    RemoteMediaSessionSnapshot? Session,
    string? FailureCode);

public sealed record RemoteMediaStatusResult(
    bool Succeeded,
    RemoteMediaSessionSnapshot? Session,
    string? FailureCode);

public sealed record RemoteMediaExchangeResult(
    bool Succeeded,
    RemoteMediaSessionSnapshot? Session,
    string? FailureCode,
    uint AcceptedSubmissionSequence,
    IReadOnlyList<RemoteMediaNegotiationMessage> DeliveredMessages,
    bool HasMore);
