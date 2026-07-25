namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostAuthorizationDecisionTests
{
    [Fact]
    public void Allow_ShouldCreateAllowedDecision()
    {
        RuntimeHostAuthorizationDecision decision =
            RuntimeHostAuthorizationDecision.Allow(
                "Explicit policy grant.");

        Assert.True(decision.IsAllowed);
        Assert.Equal(
            "Explicit policy grant.",
            decision.Reason);
    }

    [Fact]
    public void Deny_ShouldCreateDeniedDecision()
    {
        RuntimeHostAuthorizationDecision decision =
            RuntimeHostAuthorizationDecision.Deny(
                "No policy grant.");

        Assert.False(decision.IsAllowed);
        Assert.Equal(
            "No policy grant.",
            decision.Reason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Allow_InvalidReason_ShouldThrow(
        string? reason)
    {
        Assert.ThrowsAny<ArgumentException>(
            () =>
                RuntimeHostAuthorizationDecision.Allow(
                    reason!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Deny_InvalidReason_ShouldThrow(
        string? reason)
    {
        Assert.ThrowsAny<ArgumentException>(
            () =>
                RuntimeHostAuthorizationDecision.Deny(
                    reason!));
    }
}
