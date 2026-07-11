using Hase.Protocol;

namespace Hase.Protocol.Tests;

public sealed class ProtocolVersionTests
{
    [Fact]
    public void Constructor_SetsMajorAndMinorVersion()
    {
        ProtocolVersion version = new(1, 2);

        Assert.Equal((byte)1, version.Major);
        Assert.Equal((byte)2, version.Minor);
    }

    [Fact]
    public void VersionsWithSameValues_AreEqual()
    {
        ProtocolVersion first = new(1, 0);
        ProtocolVersion second = new(1, 0);

        Assert.Equal(first, second);
    }

    [Fact]
    public void VersionsWithDifferentMajorVersion_AreNotEqual()
    {
        ProtocolVersion first = new(1, 0);
        ProtocolVersion second = new(2, 0);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void VersionsWithDifferentMinorVersion_AreNotEqual()
    {
        ProtocolVersion first = new(1, 0);
        ProtocolVersion second = new(1, 1);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Current_ReturnsVersionOneZero()
    {
        ProtocolVersion version = ProtocolVersion.Current;

        Assert.Equal(new ProtocolVersion(1, 0), version);
    }

    [Fact]
    public void ToString_ReturnsMajorAndMinorVersion()
    {
        ProtocolVersion version = new(2, 3);

        Assert.Equal("2.3", version.ToString());
    }
}