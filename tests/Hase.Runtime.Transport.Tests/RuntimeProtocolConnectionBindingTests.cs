using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class RuntimeProtocolConnectionBindingTests
{
    [Fact]
    public void Create_NullTransportConnection_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = RuntimeProtocolConnectionBinding.Create(
                null!);
        }

        // Assert
        ArgumentNullException exception =
            Assert.Throws<ArgumentNullException>(
                Act);

        Assert.Equal(
            "transportConnection",
            exception.ParamName);
    }

    [Fact]
    public async Task Create_LegacyTransport_ShouldCreateLegacyBinding()
    {
        // Arrange
        var transportConnection =
            new TestLegacyTransportConnection();

        // Act
        RuntimeProtocolConnectionBinding binding =
            RuntimeProtocolConnectionBinding.Create(
                transportConnection);

        // Assert
        Assert.IsType<LegacyRuntimeProtocolConnection>(
            binding.ProtocolConnection);

        Assert.Null(
            binding.DuplexSession);

        Assert.True(
            binding.ReceivePumpCompletion.IsCompletedSuccessfully);

        await binding.DisposeAsync();

        Assert.True(
            binding.ReceivePumpCompletion.IsCompletedSuccessfully);

        await binding.DisposeAsync();
    }

    [Fact]
    public async Task Create_DuplexTransport_ShouldStartAndStopReceivePump()
    {
        // Arrange
        var transportConnection =
            new TestDuplexTransportConnection();

        // Act
        RuntimeProtocolConnectionBinding binding =
            RuntimeProtocolConnectionBinding.Create(
                transportConnection);

        await transportConnection.ReceiveStarted;

        // Assert
        DuplexRuntimeProtocolConnection protocolConnection =
            Assert.IsType<DuplexRuntimeProtocolConnection>(
                binding.ProtocolConnection);

        Assert.Same(
            binding.DuplexSession,
            protocolConnection.Session);

        Assert.NotNull(
            binding.DuplexSession);

        Assert.True(
            binding.DuplexSession!.IsRunning);

        Assert.False(
            binding.ReceivePumpCompletion.IsCompleted);

        await binding.DisposeAsync();

        Assert.True(
            transportConnection.ReceivedCancellationToken
                .IsCancellationRequested);

        Assert.True(
            binding.ReceivePumpCompletion.IsCanceled);

        Assert.False(
            binding.DuplexSession.IsRunning);

        await binding.DisposeAsync();
    }

    private sealed class TestLegacyTransportConnection
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
            throw new InvalidOperationException(
                "The legacy transport should not be used by this test.");
        }
    }

    private sealed class TestDuplexTransportConnection
        : ITransportDuplexConnection
    {
        private readonly TaskCompletionSource _receiveStarted =
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

        public Task ReceiveStarted =>
            _receiveStarted.Task;

        public CancellationToken ReceivedCancellationToken
        {
            get;
            private set;
        }

        public Task<byte[]> ExchangeAsync(
            byte[] request,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "ExchangeAsync should not be used by a duplex binding.");
        }

        public Task SendAsync(
            byte[] request,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "SendAsync should not be used by this lifecycle test.");
        }

        public async Task<byte[]> ReceiveAsync(
            CancellationToken cancellationToken = default)
        {
            ReceivedCancellationToken =
                cancellationToken;

            _receiveStarted.TrySetResult();

            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                cancellationToken);

            throw new InvalidOperationException(
                "The cancelled receive unexpectedly continued.");
        }
    }
}