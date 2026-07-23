using Hase.Runtime.Northbound;

namespace Hase.Runtime.Tests.Northbound;

public sealed class RuntimeHostApiVersionTests
{
    [Fact]
    public void Current_IsVersionOnePointZero()
    {
        Assert.Equal(
            new RuntimeHostApiVersion(
                1,
                0),
            RuntimeHostApiVersion.Current);
    }

    [Fact]
    public void ToString_ReturnsMajorAndMinor()
    {
        var version =
            new RuntimeHostApiVersion(
                2,
                3);

        Assert.Equal(
            "2.3",
            version.ToString());
    }
}