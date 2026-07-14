namespace Hase.Transport.Tests;

/// <summary>
/// Defines behavior that every request/response transport connection
/// must provide.
///
/// Concrete transport test classes supply a transport connected to the
/// provided endpoint handler.
/// </summary>
public abstract class TransportConnectionContractTests
{
    protected abstract ITransportConnection CreateTransport(
        Func<
            byte[],
            CancellationToken,
            Task<byte[]>> exchangeHandler);

    [Fact]
    public async Task ExchangeAsync_ShouldForwardRequestAndReturnResponse()
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

        ITransportConnection transport =
            CreateTransport(
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

        // Act
        byte[] actualResponse =
            await transport.ExchangeAsync(
                expectedRequest);

        // Assert
        Assert.Same(
            expectedRequest,
            receivedRequest);

        Assert.Same(
            expectedResponse,
            actualResponse);
    }

    [Fact]
    public async Task ExchangeAsync_ShouldPassCancellationTokenToEndpoint()
    {
        // Arrange
        using var cancellationTokenSource =
            new CancellationTokenSource();

        CancellationToken receivedToken =
            default;

        ITransportConnection transport =
            CreateTransport(
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
    public async Task ExchangeAsync_CancelledToken_ShouldThrowBeforeExchange()
    {
        // Arrange
        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        bool endpointCalled =
            false;

        ITransportConnection transport =
            CreateTransport(
                (
                    request,
                    cancellationToken) =>
                {
                    endpointCalled =
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
            endpointCalled);
    }

    [Fact]
    public async Task ExchangeAsync_NullRequest_ShouldThrow()
    {
        // Arrange
        ITransportConnection transport =
            CreateTransport(
                static (
                    request,
                    cancellationToken) =>
                {
                    return Task.FromResult(
                        Array.Empty<byte>());
                });

        // Act
        Task Act()
        {
            return transport.ExchangeAsync(
                null!);
        }

        // Assert
        await Assert.ThrowsAsync<
            ArgumentNullException>(
                Act);
    }
}