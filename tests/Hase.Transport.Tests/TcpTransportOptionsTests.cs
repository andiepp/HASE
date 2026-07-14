using Hase.Transport.Tcp;

namespace Hase.Transport.Tests;

public sealed class TcpTransportOptionsTests
{
    [Fact]
    public void Constructor_ShouldStoreHostAndPort()
    {
        // Arrange
        const string expectedHost =
            "192.168.1.42";

        const int expectedPort =
            5000;

        // Act
        var options =
            new TcpTransportOptions(
                expectedHost,
                expectedPort);

        // Assert
        Assert.Equal(
            expectedHost,
            options.Host);

        Assert.Equal(
            expectedPort,
            options.Port);
    }

    [Fact]
    public void Constructor_NullHost_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new TcpTransportOptions(
                null!,
                5000);
        }

        // Assert
        Assert.Throws<
            ArgumentNullException>(
                Act);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Constructor_EmptyOrWhitespaceHost_ShouldThrow(
        string host)
    {
        // Act
        void Act()
        {
            _ = new TcpTransportOptions(
                host,
                5000);
        }

        // Assert
        Assert.Throws<
            ArgumentException>(
                Act);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void Constructor_InvalidPort_ShouldThrow(
        int port)
    {
        // Act
        void Act()
        {
            _ = new TcpTransportOptions(
                "localhost",
                port);
        }

        // Assert
        ArgumentOutOfRangeException exception =
            Assert.Throws<
                ArgumentOutOfRangeException>(
                    Act);

        Assert.Equal(
            "port",
            exception.ParamName);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(65535)]
    public void Constructor_BoundaryPort_ShouldSucceed(
        int port)
    {
        // Act
        var options =
            new TcpTransportOptions(
                "localhost",
                port);

        // Assert
        Assert.Equal(
            port,
            options.Port);
    }
}