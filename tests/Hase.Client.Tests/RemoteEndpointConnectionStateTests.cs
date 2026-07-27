using Hase.Client;

namespace Hase.Client.Tests;

public sealed class RemoteEndpointConnectionStateTests
{
    [Fact]
    public void Values_AreStable()
    {
        Assert.Equal(
            0,
            (int) RemoteEndpointConnectionState.Unspecified);
        Assert.Equal(
            1,
            (int) RemoteEndpointConnectionState.Disconnected);
        Assert.Equal(
            2,
            (int) RemoteEndpointConnectionState.Connecting);
        Assert.Equal(
            3,
            (int) RemoteEndpointConnectionState.Synchronizing);
        Assert.Equal(
            4,
            (int) RemoteEndpointConnectionState.Ready);
        Assert.Equal(
            5,
            (int) RemoteEndpointConnectionState.Reconnecting);
        Assert.Equal(
            6,
            (int) RemoteEndpointConnectionState.Faulted);
    }
}
