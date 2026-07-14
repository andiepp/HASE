using Hase.Transport.Loopback;

namespace Hase.Transport.Tests;

public sealed class LoopbackTransportConnectionTests
    : TransportConnectionContractTests
{
    protected override ITransportConnection CreateTransport(
        Func<
            byte[],
            CancellationToken,
            Task<byte[]>> exchangeHandler)
    {
        return new LoopbackTransportConnection(
            exchangeHandler);
    }

    [Fact]
    public void Constructor_NullExchangeHandler_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new LoopbackTransportConnection(
                null!);
        }

        // Assert
        Assert.Throws<
            ArgumentNullException>(
                Act);
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

    [Fact]
    public async Task ExchangeAsync_HandlerException_ShouldPropagate()
    {
        // Arrange
        var expectedException =
            new InvalidOperationException(
                "Test exception");

        var transport =
            new LoopbackTransportConnection(
                (
                    request,
                    cancellationToken) =>
                {
                    throw expectedException;
                });

        // Act
        Task Act()
        {
            return transport.ExchangeAsync(
                Array.Empty<byte>());
        }

        // Assert
        InvalidOperationException actualException =
            await Assert.ThrowsAsync<
                InvalidOperationException>(
                    Act);

        Assert.Same(
            expectedException,
            actualException);
    }
}