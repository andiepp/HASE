using Hase.CompactProtocol;

namespace Hase.CompactProtocol.Tests;

public sealed class CompactProtocolVersionNotSupportedExceptionTests
{
    [Fact]
    public void Constructor_ShouldExposeVersions()
    {
        var exception =
            new CompactProtocolVersionNotSupportedException(
                actualVersion: 2,
                supportedVersion: 1);

        Assert.Equal((byte)2, exception.ActualVersion);
        Assert.Equal((byte)1, exception.SupportedVersion);
    }

    [Fact]
    public void Constructor_ShouldDescribeUnsupportedVersion()
    {
        var exception =
            new CompactProtocolVersionNotSupportedException(
                actualVersion: 2,
                supportedVersion: 1);

        Assert.Contains("2", exception.Message, StringComparison.Ordinal);
        Assert.Contains("1", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Exception_ShouldBeAnIOException()
    {
        var exception =
            new CompactProtocolVersionNotSupportedException(
                actualVersion: 2,
                supportedVersion: 1);

        Assert.IsAssignableFrom<IOException>(exception);
    }
}