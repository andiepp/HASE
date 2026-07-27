using Hase.Client;

namespace Hase.Client.Tests;

public sealed class RemoteValueKindTests
{
    [Fact]
    public void Values_AreStable()
    {
        Assert.Equal(
            0,
            (int) RemoteValueKind.Unspecified);
        Assert.Equal(
            1,
            (int) RemoteValueKind.Boolean);
        Assert.Equal(
            2,
            (int) RemoteValueKind.String);
        Assert.Equal(
            3,
            (int) RemoteValueKind.Numeric);
    }
}
