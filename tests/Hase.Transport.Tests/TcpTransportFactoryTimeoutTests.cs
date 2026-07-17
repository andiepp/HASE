using System.Net.Sockets;
using Hase.Transport.Tcp;

namespace Hase.Transport.Tests;

public sealed class TcpTransportFactoryTimeoutTests
{
    [Fact]
    public async Task ConnectAsync_ConnectionTimeout_ShouldThrowTimeoutException()
    {
        var connector =
            new PendingTcpClientConnector();

        var options =
            new TcpTransportOptions(
                "127.0.0.1",
                5000,
                TimeSpan.FromMilliseconds(
                    50));

        ITransportFactory factory =
            new TcpTransportFactory(
                options,
                maximumPayloadLength:
                    1024,
                connector);

        TimeoutException exception =
            await Assert.ThrowsAsync<TimeoutException>(
                async () => await factory.ConnectAsync());

        Assert.Contains(
            "127.0.0.1:5000",
            exception.Message);

        Assert.Contains(
            options.ConnectionTimeout.ToString(),
            exception.Message);

        Assert.IsAssignableFrom<OperationCanceledException>(
            exception.InnerException);

        Assert.Equal(
            1,
            connector.ConnectCallCount);
    }

    [Fact]
    public async Task ConnectAsync_CallerCancellation_ShouldRemainOperationCanceledException()
    {
        var connector =
            new PendingTcpClientConnector();

        var options =
            new TcpTransportOptions(
                "127.0.0.1",
                5000,
                TimeSpan.FromHours(
                    1));

        ITransportFactory factory =
            new TcpTransportFactory(
                options,
                maximumPayloadLength:
                    1024,
                connector);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task<ITransportConnection> connectTask =
            factory.ConnectAsync(
                cancellationTokenSource.Token);

        await connector.ConnectStarted;

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await connectTask);

        Assert.Equal(
            1,
            connector.ConnectCallCount);
    }

    [Fact]
    public async Task ConnectAsync_InfiniteTimeout_ShouldUseCallerTokenDirectly()
    {
        var expectedException =
            new IOException(
                "Connection failed.");

        var connector =
            new FailingTcpClientConnector(
                expectedException);

        var options =
            new TcpTransportOptions(
                "127.0.0.1",
                5000,
                Timeout.InfiniteTimeSpan);

        ITransportFactory factory =
            new TcpTransportFactory(
                options,
                maximumPayloadLength:
                    1024,
                connector);

        IOException actualException =
            await Assert.ThrowsAsync<IOException>(
                async () => await factory.ConnectAsync());

        Assert.Same(
            expectedException,
            actualException);

        Assert.Equal(
            1,
            connector.ConnectCallCount);
    }

    private sealed class PendingTcpClientConnector
        : ITcpClientConnector
    {
        private readonly TaskCompletionSource<bool>
            _connectStarted =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        public int ConnectCallCount
        {
            get;
            private set;
        }

        public Task ConnectStarted =>
            _connectStarted.Task;

        public async ValueTask ConnectAsync(
            TcpClient client,
            string host,
            int port,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(
                client);

            ConnectCallCount++;

            _connectStarted.TrySetResult(
                true);

            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                cancellationToken);
        }
    }

    private sealed class FailingTcpClientConnector
        : ITcpClientConnector
    {
        private readonly Exception _exception;

        public FailingTcpClientConnector(
            Exception exception)
        {
            _exception =
                exception
                ?? throw new ArgumentNullException(
                    nameof(exception));
        }

        public int ConnectCallCount
        {
            get;
            private set;
        }

        public ValueTask ConnectAsync(
            TcpClient client,
            string host,
            int port,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(
                client);

            cancellationToken.ThrowIfCancellationRequested();

            ConnectCallCount++;

            return ValueTask.FromException(
                _exception);
        }
    }
}
