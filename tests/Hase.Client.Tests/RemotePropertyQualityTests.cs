using Hase.Client;

namespace Hase.Client.Tests;

public sealed class RemotePropertyQualityTests
{
    [Fact]
    public void Values_AreStable()
    {
        Assert.Equal(
            0,
            (int) RemotePropertyQuality.Unspecified);
        Assert.Equal(
            1,
            (int) RemotePropertyQuality.Good);
        Assert.Equal(
            2,
            (int) RemotePropertyQuality.Uncertain);
        Assert.Equal(
            3,
            (int) RemotePropertyQuality.Bad);
    }
}
