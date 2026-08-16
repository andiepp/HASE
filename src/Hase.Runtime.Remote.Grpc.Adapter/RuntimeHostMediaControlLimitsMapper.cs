using Google.Protobuf.WellKnownTypes;
using MediaV1 = global::Hase.Runtime.Media.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Publishes the fixed local version 1 limits through the media capability
/// response without accepting remote overrides.
/// </summary>
public sealed class RuntimeHostMediaControlLimitsMapper
{
    public MediaV1.MediaControlLimits Map()
    {
        return new MediaV1.MediaControlLimits
        {
            MaximumSourceIdentityUtf8Bytes =
                RuntimeHostMediaControlLimits.MaximumSourceIdentityUtf8Bytes,
            MaximumSessionIdUtf8Bytes =
                RuntimeHostMediaControlLimits.MaximumSessionIdUtf8Bytes,
            MaximumSessionDescriptionUtf8Bytes =
                RuntimeHostMediaControlLimits
                    .MaximumSessionDescriptionUtf8Bytes,
            MaximumIceCandidateUtf8Bytes =
                RuntimeHostMediaControlLimits.MaximumIceCandidateUtf8Bytes,
            MaximumIceCandidatesPerPeer =
                RuntimeHostMediaControlLimits.MaximumIceCandidatesPerPeer,
            MaximumNegotiationMessagesPerPeer =
                RuntimeHostMediaControlLimits
                    .MaximumNegotiationMessagesPerPeer,
            MaximumPendingDeliveryMessages =
                RuntimeHostMediaControlLimits.MaximumPendingDeliveryMessages,
            MaximumNegotiationExchanges =
                RuntimeHostMediaControlLimits.MaximumNegotiationExchanges,
            NegotiationIdleTimeout =
                Duration.FromTimeSpan(
                    RuntimeHostMediaControlLimits.NegotiationIdleTimeout),
            NegotiationLifetime =
                Duration.FromTimeSpan(
                    RuntimeHostMediaControlLimits.NegotiationLifetime),
            SessionLeaseDuration =
                Duration.FromTimeSpan(
                    RuntimeHostMediaControlLimits.SessionLeaseDuration)
        };
    }
}
