using Hase.Protocol;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class LegacyRuntimeProtocolConnectionValidationTests
{
    [Fact]
    public void Constructor_NullConnection_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new LegacyRuntimeProtocolConnection(
                null!);
        }

        // Assert
        ArgumentNullException exception =
            Assert.Throws<ArgumentNullException>(
                Act);

        Assert.Equal(
            "connection",
            exception.ParamName);
    }

    [Fact]
    public async Task SendAsync_NullRequest_ShouldThrow()
    {
        // Arrange
        var connection =
            new UnusedTransportConnection();

        var protocolConnection =
            new LegacyRuntimeProtocolConnection(
                connection);

        // Act
        async Task Act()
        {
            await protocolConnection.SendAsync(
                null!,
                CancellationToken.None);
        }

        // Assert
        ArgumentNullException exception =
            await Assert.ThrowsAsync<ArgumentNullException>(
                Act);

        Assert.Equal(
            "request",
            exception.ParamName);

        Assert.Equal(
            0,
            connection.ExchangeCount);
    }

    [Fact]
    public async Task SendAsync_NonRequestRole_ShouldThrow()
    {
        // Arrange
        var connection =
            new UnusedTransportConnection();

        var protocolConnection =
            new LegacyRuntimeProtocolConnection(
                connection);

        ProtocolMessage message =
            new TestProtocolMessage(
                ProtocolMessageRole.Response,
                new CorrelationId(1));

        // Act
        async Task Act()
        {
            await protocolConnection.SendAsync(
                message,
                CancellationToken.None);
        }

        // Assert
        ArgumentException exception =
            await Assert.ThrowsAsync<ArgumentException>(
                Act);

        Assert.Equal(
            "request",
            exception.ParamName);

        Assert.Equal(
            0,
            connection.ExchangeCount);
    }

    [Fact]
    public async Task SendAsync_ZeroCorrelationIdentifier_ShouldThrow()
    {
        // Arrange
        var connection =
            new UnusedTransportConnection();

        var protocolConnection =
            new LegacyRuntimeProtocolConnection(
                connection);

        ProtocolMessage request =
            new DiscoverRequest(
                CorrelationId.None);

        // Act
        async Task Act()
        {
            await protocolConnection.SendAsync(
                request,
                CancellationToken.None);
        }

        // Assert
        ArgumentException exception =
            await Assert.ThrowsAsync<ArgumentException>(
                Act);

        Assert.Equal(
            "request",
            exception.ParamName);

        Assert.Equal(
            0,
            connection.ExchangeCount);
    }

    [Fact]
    public async Task SendAsync_Cancelled_ShouldForwardCancellationToken()
    {
        // Arrange
        var connection =
            new CancellationObservingTransportConnection();

        var protocolConnection =
            new LegacyRuntimeProtocolConnection(
                connection);

        using var cancellationSource =
            new CancellationTokenSource();

        ProtocolMessage request =
            new DiscoverRequest(
                new CorrelationId(1));

        Task<ProtocolMessage> sendTask =
            protocolConnection.SendAsync(
                request,
                cancellationSource.Token);

        await connection.ExchangeStarted;

        // Act
        cancellationSource.Cancel();

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await sendTask);

        Assert.Equal(
            cancellationSource.Token,
            connection.ReceivedCancellationToken);
    }

    private sealed record TestProtocolMessage(
        ProtocolMessageRole MessageRole,
        CorrelationId MessageCorrelationId)
        : ProtocolMessage(
            ProtocolVersion.Current,
            MessageRole,
            ProtocolMessageType.DiscoverRequest,
            MessageCorrelationId);

    private sealed class UnusedTransportConnection
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

        public int ExchangeCount
        {
            get;
            private set;
        }

        public Task<byte[]> ExchangeAsync(
            byte[] request,
            CancellationToken cancellationToken = default)
        {
            ExchangeCount++;

            throw new InvalidOperationException(
                "The transport exchange should not be reached.");
        }
    }

    private sealed class CancellationObservingTransportConnection
        : ITransportConnection
    {
        private readonly TaskCompletionSource _exchangeStarted =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

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

        public Task ExchangeStarted =>
            _exchangeStarted.Task;

        public CancellationToken ReceivedCancellationToken
        {
            get;
            private set;
        }

        public async Task<byte[]> ExchangeAsync(
            byte[] request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                request);

            ReceivedCancellationToken =
                cancellationToken;

            _exchangeStarted.TrySetResult();

            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                cancellationToken);

            throw new InvalidOperationException(
                "The cancelled exchange unexpectedly continued.");
        }
    }
}