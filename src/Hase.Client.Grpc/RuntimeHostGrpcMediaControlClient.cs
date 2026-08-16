using Hase.Client.Media;
using MediaV1 = global::Hase.Runtime.Media.Grpc.V1;

namespace Hase.Client.Grpc;

internal interface IRuntimeHostMediaGrpcTransport
{
    Task<MediaV1.GetMediaCapabilitiesResponse> GetCapabilitiesAsync(
        MediaV1.GetMediaCapabilitiesRequest request,
        CancellationToken cancellationToken);

    Task<MediaV1.StartMediaSessionResponse> StartAsync(
        MediaV1.StartMediaSessionRequest request,
        CancellationToken cancellationToken);

    Task<MediaV1.ExchangeMediaNegotiationResponse> ExchangeAsync(
        MediaV1.ExchangeMediaNegotiationRequest request,
        CancellationToken cancellationToken);

    Task<MediaV1.GetMediaSessionStatusResponse> GetStatusAsync(
        MediaV1.GetMediaSessionStatusRequest request,
        CancellationToken cancellationToken);

    Task<MediaV1.StopMediaSessionResponse> StopAsync(
        MediaV1.StopMediaSessionRequest request,
        CancellationToken cancellationToken);
}

internal sealed class RuntimeHostMediaGrpcTransport
    : IRuntimeHostMediaGrpcTransport
{
    private readonly MediaV1.RuntimeHostMediaControl.RuntimeHostMediaControlClient
        client;

    public RuntimeHostMediaGrpcTransport(
        MediaV1.RuntimeHostMediaControl.RuntimeHostMediaControlClient client)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<MediaV1.GetMediaCapabilitiesResponse>
        GetCapabilitiesAsync(
            MediaV1.GetMediaCapabilitiesRequest request,
            CancellationToken cancellationToken) =>
        await client.GetMediaCapabilitiesAsync(
                request,
                cancellationToken: cancellationToken)
            .ResponseAsync.ConfigureAwait(false);

    public async Task<MediaV1.StartMediaSessionResponse> StartAsync(
        MediaV1.StartMediaSessionRequest request,
        CancellationToken cancellationToken) =>
        await client.StartMediaSessionAsync(
                request,
                cancellationToken: cancellationToken)
            .ResponseAsync.ConfigureAwait(false);

    public async Task<MediaV1.ExchangeMediaNegotiationResponse> ExchangeAsync(
        MediaV1.ExchangeMediaNegotiationRequest request,
        CancellationToken cancellationToken) =>
        await client.ExchangeMediaNegotiationAsync(
                request,
                cancellationToken: cancellationToken)
            .ResponseAsync.ConfigureAwait(false);

    public async Task<MediaV1.GetMediaSessionStatusResponse> GetStatusAsync(
        MediaV1.GetMediaSessionStatusRequest request,
        CancellationToken cancellationToken) =>
        await client.GetMediaSessionStatusAsync(
                request,
                cancellationToken: cancellationToken)
            .ResponseAsync.ConfigureAwait(false);

    public async Task<MediaV1.StopMediaSessionResponse> StopAsync(
        MediaV1.StopMediaSessionRequest request,
        CancellationToken cancellationToken) =>
        await client.StopMediaSessionAsync(
                request,
                cancellationToken: cancellationToken)
            .ResponseAsync.ConfigureAwait(false);
}

/// <summary>
/// Authenticated Runtime Host media-control adapter. Authentication and
/// channel protection are supplied by the existing client profile/channel.
/// SDP and ICE payloads are mapped in memory and are never logged here.
/// </summary>
public sealed class RuntimeHostGrpcMediaControlClient
    : IRuntimeHostMediaControlClient
{
    private readonly IRuntimeHostMediaGrpcTransport transport;

    public RuntimeHostGrpcMediaControlClient(
        MediaV1.RuntimeHostMediaControl.RuntimeHostMediaControlClient client)
        : this(new RuntimeHostMediaGrpcTransport(client))
    {
    }

    internal RuntimeHostGrpcMediaControlClient(
        IRuntimeHostMediaGrpcTransport transport)
    {
        this.transport = transport ??
            throw new ArgumentNullException(nameof(transport));
    }

    public async Task<IReadOnlyList<RemoteMediaSourceCapability>>
        GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        MediaV1.GetMediaCapabilitiesResponse response =
            await transport.GetCapabilitiesAsync(
                new MediaV1.GetMediaCapabilitiesRequest(),
                cancellationToken).ConfigureAwait(false);
        return response.Sources.Select(Map).ToArray();
    }

    public async Task<RemoteMediaStartResult> StartAsync(
        RemoteMediaSourceTarget target,
        bool includeAudio,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        MediaV1.StartMediaSessionResponse response = await transport.StartAsync(
            new MediaV1.StartMediaSessionRequest
            {
                Target = Map(target),
                IncludeAudio = includeAudio
            },
            cancellationToken).ConfigureAwait(false);
        return new(
            IsSuccess(response.Status),
            response.Session is null ? null : Map(response.Session),
            FailureCode(response.Status));
    }

    public async Task<RemoteMediaExchangeResult> ExchangeAsync(
        string sessionId,
        uint acknowledgedDeliverySequence,
        RemoteMediaNegotiationMessage? submittedMessage,
        CancellationToken cancellationToken = default)
    {
        var request = new MediaV1.ExchangeMediaNegotiationRequest
        {
            SessionId = sessionId,
            AcknowledgedDeliverySequence = acknowledgedDeliverySequence
        };
        if (submittedMessage is not null)
        {
            request.SubmittedMessage = Map(submittedMessage);
        }

        MediaV1.ExchangeMediaNegotiationResponse response =
            await transport.ExchangeAsync(request, cancellationToken)
                .ConfigureAwait(false);
        return new(
            IsSuccess(response.Status),
            response.Session is null ? null : Map(response.Session),
            FailureCode(response.Status),
            response.AcceptedSubmissionSequence,
            response.DeliveredMessages.Select(Map).ToArray(),
            response.HasMore);
    }

    public async Task<RemoteMediaStatusResult> GetStatusAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        MediaV1.GetMediaSessionStatusResponse response =
            await transport.GetStatusAsync(
                new MediaV1.GetMediaSessionStatusRequest
                {
                    SessionId = sessionId
                },
                cancellationToken).ConfigureAwait(false);
        return new(
            IsSuccess(response.Status),
            response.Session is null ? null : Map(response.Session),
            FailureCode(response.Status));
    }

    public async Task<RemoteMediaStopResult> StopAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        MediaV1.StopMediaSessionResponse response = await transport.StopAsync(
            new MediaV1.StopMediaSessionRequest { SessionId = sessionId },
            cancellationToken).ConfigureAwait(false);
        return new(
            IsSuccess(response.Status),
            response.Session is null ? null : Map(response.Session),
            FailureCode(response.Status));
    }

    private static RemoteMediaSourceCapability Map(
        MediaV1.MediaSourceCapability source) => new(
        Map(source.Target),
        source.DisplayName,
        source.Availability switch
        {
            MediaV1.MediaSourceAvailability.Idle =>
                RemoteMediaSourceAvailability.Idle,
            MediaV1.MediaSourceAvailability.Busy =>
                RemoteMediaSourceAvailability.Busy,
            MediaV1.MediaSourceAvailability.Faulted =>
                RemoteMediaSourceAvailability.Faulted,
            _ => RemoteMediaSourceAvailability.Unavailable
        },
        source.SupportsVideo,
        source.SupportsAudio);

    private static RemoteMediaSourceTarget Map(MediaV1.MediaSourceTarget source)
        => new(source.MediaSourceId, source.MediaSourceGeneration);

    private static MediaV1.MediaSourceTarget Map(RemoteMediaSourceTarget source)
        => new()
        {
            MediaSourceId = source.MediaSourceId,
            MediaSourceGeneration = source.MediaSourceGeneration
        };

    private static RemoteMediaNegotiationMessage Map(
        MediaV1.MediaNegotiationMessage source) => new(
        source.Sequence,
        source.Kind switch
        {
            MediaV1.MediaNegotiationMessageKind.Offer =>
                RemoteMediaNegotiationKind.Offer,
            MediaV1.MediaNegotiationMessageKind.Answer =>
                RemoteMediaNegotiationKind.Answer,
            MediaV1.MediaNegotiationMessageKind.IceCandidate =>
                RemoteMediaNegotiationKind.IceCandidate,
            MediaV1.MediaNegotiationMessageKind.IceComplete =>
                RemoteMediaNegotiationKind.IceComplete,
            _ => throw new InvalidOperationException(
                "The Runtime Host returned an unsupported negotiation kind.")
        },
        source.SensitivePayload);

    private static MediaV1.MediaNegotiationMessage Map(
        RemoteMediaNegotiationMessage source) => new()
        {
            Sequence = source.Sequence,
            Kind = source.Kind switch
            {
                RemoteMediaNegotiationKind.Offer =>
                    MediaV1.MediaNegotiationMessageKind.Offer,
                RemoteMediaNegotiationKind.Answer =>
                    MediaV1.MediaNegotiationMessageKind.Answer,
                RemoteMediaNegotiationKind.IceCandidate =>
                    MediaV1.MediaNegotiationMessageKind.IceCandidate,
                RemoteMediaNegotiationKind.IceComplete =>
                    MediaV1.MediaNegotiationMessageKind.IceComplete,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(source), source.Kind,
                    "A supported negotiation kind is required.")
            },
            SensitivePayload = source.SensitivePayload
        };

    private static RemoteMediaSessionSnapshot Map(
        MediaV1.MediaSessionSnapshot source) => new(
        source.SessionId,
        Map(source.Target),
        source.AudioRequested,
        source.State switch
        {
            MediaV1.MediaSessionState.Starting =>
                RemoteMediaSessionState.Starting,
            MediaV1.MediaSessionState.Negotiating =>
                RemoteMediaSessionState.Negotiating,
            MediaV1.MediaSessionState.Streaming =>
                RemoteMediaSessionState.Streaming,
            MediaV1.MediaSessionState.Stopping =>
                RemoteMediaSessionState.Stopping,
            MediaV1.MediaSessionState.Ended => RemoteMediaSessionState.Ended,
            _ => RemoteMediaSessionState.Faulted
        },
        source.TerminalReason switch
        {
            MediaV1.MediaSessionTerminalReason.ClientStopped =>
                RemoteMediaTerminalReason.ClientStopped,
            MediaV1.MediaSessionTerminalReason.ControlDisconnected =>
                RemoteMediaTerminalReason.ControlDisconnected,
            MediaV1.MediaSessionTerminalReason.LeaseExpired =>
                RemoteMediaTerminalReason.LeaseExpired,
            MediaV1.MediaSessionTerminalReason.NegotiationTimedOut =>
                RemoteMediaTerminalReason.NegotiationTimedOut,
            MediaV1.MediaSessionTerminalReason.SourceLost =>
                RemoteMediaTerminalReason.SourceLost,
            MediaV1.MediaSessionTerminalReason.MediaBoundaryFailed =>
                RemoteMediaTerminalReason.MediaBoundaryFailed,
            MediaV1.MediaSessionTerminalReason.HostStopping =>
                RemoteMediaTerminalReason.HostStopping,
            MediaV1.MediaSessionTerminalReason.ProtocolRejected =>
                RemoteMediaTerminalReason.ProtocolRejected,
            _ => RemoteMediaTerminalReason.None
        },
        MapTimestamp(source.StartedAtUtc),
        MapTimestamp(source.LastTransitionAtUtc),
        MapTimestamp(source.LeaseExpiresAtUtc));

    private static DateTimeOffset? MapTimestamp(
        Google.Protobuf.WellKnownTypes.Timestamp? value) =>
        value is null ? null : value.ToDateTimeOffset();

    private static bool IsSuccess(MediaV1.MediaControlOperationStatus status) =>
        status == MediaV1.MediaControlOperationStatus.Success;

    private static string? FailureCode(
        MediaV1.MediaControlOperationStatus status) => status switch
        {
            MediaV1.MediaControlOperationStatus.Success => null,
            MediaV1.MediaControlOperationStatus.SourceNotCurrent =>
                "source-not-current",
            MediaV1.MediaControlOperationStatus.SourceUnavailable =>
                "source-unavailable",
            MediaV1.MediaControlOperationStatus.SessionBusy => "session-busy",
            MediaV1.MediaControlOperationStatus.AudioNotSupported =>
                "audio-not-supported",
            MediaV1.MediaControlOperationStatus.SessionNotFound =>
                "session-not-found",
            MediaV1.MediaControlOperationStatus.SessionNotOwned =>
                "session-not-owned",
            MediaV1.MediaControlOperationStatus.InvalidState => "invalid-state",
            MediaV1.MediaControlOperationStatus.LimitExceeded =>
                "limit-exceeded",
            MediaV1.MediaControlOperationStatus.TimedOut => "timed-out",
            MediaV1.MediaControlOperationStatus.Faulted => "faulted",
            _ => "invalid-request"
        };
}
