using Hase.Client;

namespace Hase.Client.Tests;

public sealed class RuntimeHostClientApiVersionTests
{
    [Fact]
    public void Current_IsVersionOnePointZero()
    {
        Assert.Equal(
            new RuntimeHostClientApiVersion(
                1,
                0),
            RuntimeHostClientApiVersion.Current);
    }

    [Fact]
    public void Constructor_ZeroMajor_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            "major",
            () => new RuntimeHostClientApiVersion(
                0,
                1));
    }

    [Fact]
    public void ToString_ReturnsMajorAndMinor()
    {
        var version =
            new RuntimeHostClientApiVersion(
                2,
                3);

        Assert.Equal(
            "2.3",
            version.ToString());
    }
}
