namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostAuthenticationFailureReasonTests
{
    [Fact]
    public void None_ShouldBeDefault()
    {
        Assert.Equal(
            RuntimeHostAuthenticationFailureReason.None,
            default);
    }

    [Fact]
    public void Values_ShouldRemainDistinct()
    {
        Assert.NotEqual(
            RuntimeHostAuthenticationFailureReason.None,
            RuntimeHostAuthenticationFailureReason.UnknownCredential);
    }
}
