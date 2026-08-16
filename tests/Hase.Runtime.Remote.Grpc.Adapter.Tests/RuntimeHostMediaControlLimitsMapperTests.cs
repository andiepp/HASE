using MediaV1 = global::Hase.Runtime.Media.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostMediaControlLimitsMapperTests
{
    [Fact]
    public void Map_ShouldPublishEveryFixedVersionOneLimit()
    {
        RuntimeHostMediaControlLimitsMapper mapper =
            new();

        MediaV1.MediaControlLimits limits =
            mapper.Map();

        Assert.Equal(
            (uint)RuntimeHostMediaControlLimits
                .MaximumSourceIdentityUtf8Bytes,
            limits.MaximumSourceIdentityUtf8Bytes);
        Assert.Equal(
            (uint)RuntimeHostMediaControlLimits.MaximumSessionIdUtf8Bytes,
            limits.MaximumSessionIdUtf8Bytes);
        Assert.Equal(
            (uint)RuntimeHostMediaControlLimits
                .MaximumSessionDescriptionUtf8Bytes,
            limits.MaximumSessionDescriptionUtf8Bytes);
        Assert.Equal(
            (uint)RuntimeHostMediaControlLimits
                .MaximumIceCandidateUtf8Bytes,
            limits.MaximumIceCandidateUtf8Bytes);
        Assert.Equal(
            (uint)RuntimeHostMediaControlLimits
                .MaximumIceCandidatesPerPeer,
            limits.MaximumIceCandidatesPerPeer);
        Assert.Equal(
            (uint)RuntimeHostMediaControlLimits
                .MaximumNegotiationMessagesPerPeer,
            limits.MaximumNegotiationMessagesPerPeer);
        Assert.Equal(
            (uint)RuntimeHostMediaControlLimits
                .MaximumPendingDeliveryMessages,
            limits.MaximumPendingDeliveryMessages);
        Assert.Equal(
            (uint)RuntimeHostMediaControlLimits
                .MaximumNegotiationExchanges,
            limits.MaximumNegotiationExchanges);
        Assert.Equal(
            RuntimeHostMediaControlLimits.NegotiationIdleTimeout,
            limits.NegotiationIdleTimeout.ToTimeSpan());
        Assert.Equal(
            RuntimeHostMediaControlLimits.NegotiationLifetime,
            limits.NegotiationLifetime.ToTimeSpan());
        Assert.Equal(
            RuntimeHostMediaControlLimits.SessionLeaseDuration,
            limits.SessionLeaseDuration.ToTimeSpan());
    }
}
