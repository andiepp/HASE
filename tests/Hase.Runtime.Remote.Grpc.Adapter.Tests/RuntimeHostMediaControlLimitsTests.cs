namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostMediaControlLimitsTests
{
    [Fact]
    public void VersionOneLimits_ShouldHaveStableValues()
    {
        Assert.Equal(
            128,
            RuntimeHostMediaControlLimits.MaximumSourceIdentityUtf8Bytes);
        Assert.Equal(
            128,
            RuntimeHostMediaControlLimits.MaximumSessionIdUtf8Bytes);
        Assert.Equal(
            49_152,
            RuntimeHostMediaControlLimits
                .MaximumSessionDescriptionUtf8Bytes);
        Assert.Equal(
            4_096,
            RuntimeHostMediaControlLimits.MaximumIceCandidateUtf8Bytes);
        Assert.Equal(
            32,
            RuntimeHostMediaControlLimits.MaximumIceCandidatesPerPeer);
        Assert.Equal(
            36,
            RuntimeHostMediaControlLimits
                .MaximumNegotiationMessagesPerPeer);
        Assert.Equal(
            16,
            RuntimeHostMediaControlLimits.MaximumPendingDeliveryMessages);
        Assert.Equal(
            128,
            RuntimeHostMediaControlLimits.MaximumNegotiationExchanges);
        Assert.Equal(
            TimeSpan.FromSeconds(15),
            RuntimeHostMediaControlLimits.NegotiationIdleTimeout);
        Assert.Equal(
            TimeSpan.FromSeconds(60),
            RuntimeHostMediaControlLimits.NegotiationLifetime);
        Assert.Equal(
            TimeSpan.FromSeconds(30),
            RuntimeHostMediaControlLimits.SessionLeaseDuration);
    }
}
