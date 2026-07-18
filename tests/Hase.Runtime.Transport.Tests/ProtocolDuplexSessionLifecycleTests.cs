using Hase.Protocol;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class ProtocolDuplexSessionLifecycleTests
{
    [Fact]
    public async Task SendAsync_BeforeRun_ShouldThrow()
    {
        // Arrange
        var connection =
            new TestDuplexTransportConnection();

        var session =
            new ProtocolDuplexSession(
                connection);

        var request =
            new DiscoverRequest(
                new CorrelationId(
                    1));

        // Act
        Task<ProtocolMessage> Act()
        {
            return session.SendAsync(
                request);
        }

        // Assert
        InvalidOperationException exception =
            await Assert.ThrowsAsync<
                InvalidOperationException>(
                    Act);

        Assert.Equal(
            "The protocol duplex session receive pump "
            + "is not running.",
            exception.Message);

        Assert.False(
            session.IsRunning);
    }

    [Fact]
    public async Task RunAsync_Cancellation_ShouldStopSession()
    {
        // Arrange
        var connection =
            new TestDuplexTransportConnection();

        var session =
            new ProtocolDuplexSession(
                connection);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task runTask =
            session.RunAsync(
                cancellationTokenSource.Token);

        Assert.True(
            session.IsRunning);

        // Act
        cancellationTokenSource.Cancel();

        // Assert
        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(
                async () => await runTask);

        Assert.False(
            session.IsRunning);
    }

    [Fact]
    public async Task RunAsync_AfterStop_ShouldThrowAndSendShouldRemainUnavailable()
    {
        // Arrange
        var connection =
            new TestDuplexTransportConnection();

        var session =
            new ProtocolDuplexSession(
                connection);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task firstRunTask =
            session.RunAsync(
                cancellationTokenSource.Token);

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(
                async () => await firstRunTask);

        var request =
            new DiscoverRequest(
                new CorrelationId(
                    2));

        // Act
        Task RunAgain()
        {
            return session.RunAsync();
        }

        Task<ProtocolMessage> SendAfterStop()
        {
            return session.SendAsync(
                request);
        }

        // Assert
        InvalidOperationException runException =
            await Assert.ThrowsAsync<
                InvalidOperationException>(
                    RunAgain);

        Assert.Equal(
            "The protocol duplex session receive pump "
            + "can be started only once.",
            runException.Message);

        InvalidOperationException sendException =
            await Assert.ThrowsAsync<
                InvalidOperationException>(
                    SendAfterStop);

        Assert.Equal(
            "The protocol duplex session receive pump "
            + "is not running.",
            sendException.Message);

        Assert.False(
            session.IsRunning);
    }

    private sealed class TestDuplexTransportConnection
        : ITransportDuplexConnection
    {
        public event EventHandler<
            TransportConnectionStateChangedEventArgs>?
            StateChanged;

        public TransportConnectionState State =>
            TransportConnectionState.Connected;

        public Task<byte[]> ExchangeAsync(
            byte[] request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException(
                "The test connection uses duplex operations.");
        }

        public Task SendAsync(
            byte[] payload,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                payload);

            cancellationToken.ThrowIfCancellationRequested();

            return Task.CompletedTask;
        }

        public async Task<byte[]> ReceiveAsync(
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                cancellationToken);

            return [];
        }

        public void RaiseStateChanged(
            TransportConnectionState previousState,
            TransportConnectionState currentState)
        {
            StateChanged?.Invoke(
                this,
                new TransportConnectionStateChangedEventArgs(
                    previousState,
                    currentState));
        }
    }
}