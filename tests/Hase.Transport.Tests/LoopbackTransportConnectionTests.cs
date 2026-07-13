using Hase.Transport.Loopback;

namespace Hase.Transport.Tests;

public sealed class LoopbackTransportConnectionTests
{
    [Fact]
    public void ImplementsTransportConnection()
    {
        // Arrange
        var transport =
            new LoopbackTransportConnection(
                static (
                    request,
                    cancellationToken) =>
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();

                    return Task.FromResult(
                        request);
                });

        // Assert
        Assert.IsAssignableFrom<ITransportConnection>(
            transport);
    }

    [Fact]
    public async Task ExchangeAsync_ShouldForwardRequestToHandler()
    {
        // Arrange
        byte[] expectedRequest =
        [
            0x01,
            0x02,
            0x03
        ];

        byte[]? receivedRequest =
            null;

        var transport =
            new LoopbackTransportConnection(
                (
                    request,
                    cancellationToken) =>
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();

                    receivedRequest =
                        request;

                    return Task.FromResult(
                        new byte[]
                        {
                            0x10,
                            0x20
                        });
                });

        // Act
        byte[] response =
            await transport.ExchangeAsync(
                expectedRequest);

        // Assert
        Assert.Same(
            expectedRequest,
            receivedRequest);

        Assert.Equal(
            new byte[]
            {
                0x10,
                0x20
            },
            response);
    }

    [Fact]
    public async Task ExchangeAsync_ShouldPassCancellationTokenToHandler()
    {
        // Arrange
        using var cancellationTokenSource =
            new CancellationTokenSource();

        CancellationToken receivedToken =
            default;

        var transport =
            new LoopbackTransportConnection(
                (
                    request,
                    cancellationToken) =>
                {
                    receivedToken =
                        cancellationToken;

                    return Task.FromResult(
                        Array.Empty<byte>());
                });

        // Act
        await transport.ExchangeAsync(
            Array.Empty<byte>(),
            cancellationTokenSource.Token);

        // Assert
        Assert.Equal(
            cancellationTokenSource.Token,
            receivedToken);
    }

    [Fact]
    public async Task ExchangeAsync_CancelledToken_ShouldThrow()
    {
        // Arrange
        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        bool handlerCalled =
            false;

        var transport =
            new LoopbackTransportConnection(
                (
                    request,
                    cancellationToken) =>
                {
                    handlerCalled =
                        true;

                    return Task.FromResult(
                        Array.Empty<byte>());
                });

        // Act
        Task Act()
        {
            return transport.ExchangeAsync(
                Array.Empty<byte>(),
                cancellationTokenSource.Token);
        }

        // Assert
        await Assert.ThrowsAsync<
            OperationCanceledException>(
                Act);

        Assert.False(
            handlerCalled);
    }

    [Fact]
    public async Task ExchangeAsync_NullResponse_ShouldThrow()
    {
        // Arrange
        var transport =
            new LoopbackTransportConnection(
                static (
                    request,
                    cancellationToken) =>
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();

                    return Task.FromResult<byte[]>(
                        null!);
                });

        // Act
        Task Act()
        {
            return transport.ExchangeAsync(
                Array.Empty<byte>());
        }

        // Assert
        InvalidOperationException exception =
            await Assert.ThrowsAsync<
                InvalidOperationException>(
                    Act);

        Assert.Equal(
            "The loopback endpoint handler returned a null response.",
            exception.Message);
    }
}