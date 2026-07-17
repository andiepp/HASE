using Hase.Transport.Tcp;

namespace Hase.Transport.Tests;

public sealed class TcpTransportConnectionTimeoutOptionsTests
{
    [Fact]
    public void Constructor_WithoutTimeout_ShouldUseDefaultTimeout()
    {
        var options =
            new TcpTransportOptions(
                "127.0.0.1",
                5000);

        Assert.Equal(
            TimeSpan.FromSeconds(
                5),
            TcpTransportOptions.DefaultConnectionTimeout);

        Assert.Equal(
            TcpTransportOptions.DefaultConnectionTimeout,
            options.ConnectionTimeout);
    }

    [Fact]
    public void Constructor_WithExplicitTimeout_ShouldPreserveTimeout()
    {
        TimeSpan timeout =
            TimeSpan.FromSeconds(
                3);

        var options =
            new TcpTransportOptions(
                "127.0.0.1",
                5000,
                timeout);

        Assert.Equal(
            timeout,
            options.ConnectionTimeout);
    }

    [Fact]
    public void Constructor_WithInfiniteTimeout_ShouldPreserveInfiniteTimeout()
    {
        var options =
            new TcpTransportOptions(
                "127.0.0.1",
                5000,
                Timeout.InfiniteTimeSpan);

        Assert.Equal(
            Timeout.InfiniteTimeSpan,
            options.ConnectionTimeout);
    }

    [Fact]
    public void Constructor_WithZeroTimeout_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new TcpTransportOptions(
                    "127.0.0.1",
                    5000,
                    TimeSpan.Zero));
    }

    [Theory]
    [InlineData(-2)]
    [InlineData(-10)]
    [InlineData(-1000)]
    public void Constructor_WithUnsupportedNegativeTimeout_ShouldThrow(
        int timeoutMilliseconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new TcpTransportOptions(
                    "127.0.0.1",
                    5000,
                    TimeSpan.FromMilliseconds(
                        timeoutMilliseconds)));
    }
}