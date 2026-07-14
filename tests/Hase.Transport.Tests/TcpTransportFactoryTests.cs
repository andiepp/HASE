using Hase.Transport.Tcp;

namespace Hase.Transport.Tests;

public sealed class TcpTransportFactoryTests
{
    [Fact]
    public async Task ConnectAsync_ShouldConnectAndReturnWorkingTransport()
    {
        // Arrange
        byte[] expectedRequest =
        [
            0x01,
            0x02,
            0x03
        ];

        byte[] expectedResponse =
        [
            0x10,
            0x20
        ];

        byte[]? receivedRequest =
            null;

        await using var server =
            new FramedTcpTestServer();

        Task serverTask =
            server.ServeSingleExchangeAsync(
                (
                    request,
                    cancellationToken) =>
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();

                    receivedRequest =
                        request;

                    return Task.FromResult(
                        expectedResponse);
                });

        var options =
            new TcpTransportOptions(
                "127.0.0.1",
                server.Port);

        ITransportFactory factory =
            new TcpTransportFactory(
                options,
                maximumPayloadLength: 1024);

        // Act
        ITransportConnection connection =
            await factory.ConnectAsync();

        try
        {
            byte[] actualResponse =
                await connection.ExchangeAsync(
                    expectedRequest);

            await serverTask;

            // Assert
            Assert.Equal(
                expectedRequest,
                receivedRequest);

            Assert.Equal(
                expectedResponse,
                actualResponse);
        }
        finally
        {
            if (connection is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task ConnectAsync_CancelledToken_ShouldThrow()
    {
        // Arrange
        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        var options =
            new TcpTransportOptions(
                "127.0.0.1",
                5000);

        ITransportFactory factory =
            new TcpTransportFactory(
                options,
                maximumPayloadLength: 1024);

        // Act
        Task Act()
        {
            return factory.ConnectAsync(
                cancellationTokenSource.Token);
        }

        // Assert
        await Assert.ThrowsAsync<
            OperationCanceledException>(
                Act);
    }

    [Fact]
    public void Constructor_NullOptions_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new TcpTransportFactory(
                null!,
                maximumPayloadLength: 1024);
        }

        // Assert
        Assert.Throws<
            ArgumentNullException>(
                Act);
    }

    [Fact]
    public void Constructor_NegativeMaximumPayloadLength_ShouldThrow()
    {
        // Arrange
        var options =
            new TcpTransportOptions(
                "localhost",
                5000);

        // Act
        void Act()
        {
            _ = new TcpTransportFactory(
                options,
                maximumPayloadLength: -1);
        }

        // Assert
        ArgumentOutOfRangeException exception =
            Assert.Throws<
                ArgumentOutOfRangeException>(
                    Act);

        Assert.Equal(
            "maximumPayloadLength",
            exception.ParamName);
    }
}