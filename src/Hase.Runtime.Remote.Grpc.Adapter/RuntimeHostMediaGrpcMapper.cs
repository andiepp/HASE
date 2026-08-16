using Google.Protobuf.WellKnownTypes;
using Hase.Runtime.Media;
using MediaV1 = global::Hase.Runtime.Media.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps the transport-neutral media domain to the sanitized version 1 wire
/// contract without projecting local device or network identities.
/// </summary>
public sealed class RuntimeHostMediaGrpcMapper
{
    public RuntimeHostMediaSourceTarget Map(MediaV1.MediaSourceTarget source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new(source.MediaSourceId, source.MediaSourceGeneration);
    }

    public RuntimeHostMediaNegotiationMessage Map(
        MediaV1.MediaNegotiationMessage source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new(
            source.Sequence,
            source.Kind switch
            {
                MediaV1.MediaNegotiationMessageKind.Offer =>
                    RuntimeHostMediaNegotiationKind.Offer,
                MediaV1.MediaNegotiationMessageKind.Answer =>
                    RuntimeHostMediaNegotiationKind.Answer,
                MediaV1.MediaNegotiationMessageKind.IceCandidate =>
                    RuntimeHostMediaNegotiationKind.IceCandidate,
                MediaV1.MediaNegotiationMessageKind.IceComplete =>
                    RuntimeHostMediaNegotiationKind.IceComplete,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(source), source.Kind,
                    "A supported negotiation kind is required.")
            },
            source.SensitivePayload);
    }

    public MediaV1.MediaNegotiationMessage Map(
        RuntimeHostMediaNegotiationMessage source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new()
        {
            Sequence = source.Sequence,
            Kind = source.Kind switch
            {
                RuntimeHostMediaNegotiationKind.Offer =>
                    MediaV1.MediaNegotiationMessageKind.Offer,
                RuntimeHostMediaNegotiationKind.Answer =>
                    MediaV1.MediaNegotiationMessageKind.Answer,
                RuntimeHostMediaNegotiationKind.IceCandidate =>
                    MediaV1.MediaNegotiationMessageKind.IceCandidate,
                RuntimeHostMediaNegotiationKind.IceComplete =>
                    MediaV1.MediaNegotiationMessageKind.IceComplete,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(source), source.Kind,
                    "A supported negotiation kind is required.")
            },
            SensitivePayload = source.SensitivePayload
        };
    }

    public MediaV1.MediaSessionSnapshot Map(
        RuntimeHostMediaSessionSnapshot source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new()
        {
            SessionId = source.SessionId,
            Target = new MediaV1.MediaSourceTarget
            {
                MediaSourceId = source.Target.MediaSourceId,
                MediaSourceGeneration = source.Target.MediaSourceGeneration
            },
            AudioRequested = source.AudioRequested,
            State = source.State switch
            {
                RuntimeHostMediaSessionState.Starting =>
                    MediaV1.MediaSessionState.Starting,
                RuntimeHostMediaSessionState.Negotiating =>
                    MediaV1.MediaSessionState.Negotiating,
                RuntimeHostMediaSessionState.Streaming =>
                    MediaV1.MediaSessionState.Streaming,
                RuntimeHostMediaSessionState.Stopping =>
                    MediaV1.MediaSessionState.Stopping,
                RuntimeHostMediaSessionState.Ended =>
                    MediaV1.MediaSessionState.Ended,
                RuntimeHostMediaSessionState.Faulted =>
                    MediaV1.MediaSessionState.Faulted,
                _ => MediaV1.MediaSessionState.Unspecified
            },
            StartedAtUtc = Timestamp.FromDateTimeOffset(source.StartedAtUtc),
            LastTransitionAtUtc =
                Timestamp.FromDateTimeOffset(source.LastTransitionAtUtc),
            LeaseExpiresAtUtc =
                Timestamp.FromDateTimeOffset(source.LeaseExpiresAtUtc),
            TerminalReason = source.TerminalReason switch
            {
                RuntimeHostMediaTerminalReason.ClientStopped =>
                    MediaV1.MediaSessionTerminalReason.ClientStopped,
                RuntimeHostMediaTerminalReason.ControlDisconnected =>
                    MediaV1.MediaSessionTerminalReason.ControlDisconnected,
                RuntimeHostMediaTerminalReason.LeaseExpired =>
                    MediaV1.MediaSessionTerminalReason.LeaseExpired,
                RuntimeHostMediaTerminalReason.NegotiationTimedOut =>
                    MediaV1.MediaSessionTerminalReason.NegotiationTimedOut,
                RuntimeHostMediaTerminalReason.SourceLost =>
                    MediaV1.MediaSessionTerminalReason.SourceLost,
                RuntimeHostMediaTerminalReason.MediaBoundaryFailed =>
                    MediaV1.MediaSessionTerminalReason.MediaBoundaryFailed,
                RuntimeHostMediaTerminalReason.HostStopping =>
                    MediaV1.MediaSessionTerminalReason.HostStopping,
                RuntimeHostMediaTerminalReason.ProtocolRejected =>
                    MediaV1.MediaSessionTerminalReason.ProtocolRejected,
                _ => MediaV1.MediaSessionTerminalReason.Unspecified
            }
        };
    }

    public MediaV1.MediaControlOperationStatus Map(
        RuntimeHostMediaOperationStatus source) => source switch
        {
            RuntimeHostMediaOperationStatus.Success =>
                MediaV1.MediaControlOperationStatus.Success,
            RuntimeHostMediaOperationStatus.InvalidRequest =>
                MediaV1.MediaControlOperationStatus.InvalidRequest,
            RuntimeHostMediaOperationStatus.SourceNotCurrent =>
                MediaV1.MediaControlOperationStatus.SourceNotCurrent,
            RuntimeHostMediaOperationStatus.SourceUnavailable =>
                MediaV1.MediaControlOperationStatus.SourceUnavailable,
            RuntimeHostMediaOperationStatus.SessionBusy =>
                MediaV1.MediaControlOperationStatus.SessionBusy,
            RuntimeHostMediaOperationStatus.AudioNotSupported =>
                MediaV1.MediaControlOperationStatus.AudioNotSupported,
            RuntimeHostMediaOperationStatus.SessionNotFound =>
                MediaV1.MediaControlOperationStatus.SessionNotFound,
            RuntimeHostMediaOperationStatus.SessionNotOwned =>
                MediaV1.MediaControlOperationStatus.SessionNotOwned,
            RuntimeHostMediaOperationStatus.InvalidState =>
                MediaV1.MediaControlOperationStatus.InvalidState,
            RuntimeHostMediaOperationStatus.LimitExceeded =>
                MediaV1.MediaControlOperationStatus.LimitExceeded,
            RuntimeHostMediaOperationStatus.TimedOut =>
                MediaV1.MediaControlOperationStatus.TimedOut,
            RuntimeHostMediaOperationStatus.Faulted =>
                MediaV1.MediaControlOperationStatus.Faulted,
            _ => MediaV1.MediaControlOperationStatus.Unspecified
        };
}
