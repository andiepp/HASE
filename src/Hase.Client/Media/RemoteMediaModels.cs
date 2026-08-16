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

public sealed record RemoteMediaSourceTarget(
    string MediaSourceId,
    string MediaSourceGeneration);

public sealed record RemoteMediaSourceCapability(
    RemoteMediaSourceTarget Target,
    string DisplayName,
    RemoteMediaSourceAvailability Availability,
    bool SupportsVideo,
    bool SupportsAudio);

public sealed record RemoteMediaSessionSnapshot(
    string SessionId,
    RemoteMediaSourceTarget Target,
    bool AudioRequested,
    RemoteMediaSessionState State);

public sealed record RemoteMediaStartResult(
    bool Succeeded,
    RemoteMediaSessionSnapshot? Session,
    string? FailureCode);

public sealed record RemoteMediaStopResult(
    bool Succeeded,
    RemoteMediaSessionSnapshot? Session,
    string? FailureCode);
