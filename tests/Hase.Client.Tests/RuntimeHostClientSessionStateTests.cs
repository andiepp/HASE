using Hase.Client;

namespace Hase.Client.Tests;

public sealed class RuntimeHostClientSessionStateTests
{
    [Fact]
    public void Values_AreStable()
    {
        Assert.Equal(
            0,
            (int) RuntimeHostClientSessionState.Unspecified);
        Assert.Equal(
            1,
            (int) RuntimeHostClientSessionState.Disconnected);
        Assert.Equal(
            2,
            (int) RuntimeHostClientSessionState.Connecting);
        Assert.Equal(
            3,
            (int) RuntimeHostClientSessionState.Connected);
        Assert.Equal(
            4,
            (int) RuntimeHostClientSessionState.Reconnecting);
        Assert.Equal(
            5,
            (int) RuntimeHostClientSessionState.Disconnecting);
        Assert.Equal(
            6,
            (int) RuntimeHostClientSessionState.Faulted);
    }
}
