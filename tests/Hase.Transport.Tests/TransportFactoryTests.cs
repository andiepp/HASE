namespace Hase.Transport.Tests;

public sealed class TransportFactoryTests
{
    private sealed class TestTransportFactory
        : ITransportFactory
    {
        public Task<ITransportConnection> ConnectAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ITransportConnection connection =
                new TestTransportConnection();

            return Task.FromResult(
                connection);
        }
    }

    private sealed class TestTransportConnection
        : ITransportConnection
    {
        public event EventHandler<
            TransportConnectionStateChangedEventArgs>?
            StateChanged
        {
            add
            {
            }

            remove
            {
            }
        }

        public TransportConnectionState State =>
            TransportConnectionState.Connected;

        public Task<byte[]> ExchangeAsync(
            byte[] request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                request);

            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                request);
        }
    }

    [Fact]
    public async Task ConnectAsync_ShouldReturnTransportConnection()
    {
        // Arrange
        ITransportFactory factory =
            new TestTransportFactory();

        // Act
        ITransportConnection connection =
            await factory.ConnectAsync();

        // Assert
        Assert.NotNull(
            connection);

        Assert.IsAssignableFrom<ITransportConnection>(
            connection);
    }

    [Fact]
    public async Task ConnectAsync_CancelledToken_ShouldThrow()
    {
        // Arrange
        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        ITransportFactory factory =
            new TestTransportFactory();

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
}