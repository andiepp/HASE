using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class
    RuntimeEndpointConnectionCoordinatorDuplexLifecycleTests
{
    [Fact]
    public async Task ConnectAsync_DuplexTransport_ShouldOwnReceivePump()
    {
        // Arrange
        var transportConnection =
            new TestDuplexTransportConnection();

        var transportFactory =
            new TestTransportFactory(
                transportConnection);

        await using var connectionManager =
            new TransportConnectionManager(
                transportFactory);

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint();

        var synchronizer =
            new DualContractSynchronizer();

        var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                connectionManager,
                runtimeEndpoint,
                synchronizer);

        // Act
        ITransportConnection result =
            await coordinator.ConnectAsync();

        await transportConnection.ReceiveStarted;

        // Assert
        Assert.Same(
            transportConnection,
            result);

        Assert.Equal(
            0,
            synchronizer.TransportSynchronizationCount);

        Assert.Equal(
            1,
            synchronizer.ProtocolSynchronizationCount);

        DuplexRuntimeProtocolConnection protocolConnection =
            Assert.IsType<DuplexRuntimeProtocolConnection>(
                synchronizer.ReceivedProtocolConnection);

        Assert.True(
            protocolConnection.Session.IsRunning);

        Assert.Equal(
            EndpointConnectionState.Ready,
            runtimeEndpoint.ConnectionStatus.State);

        Assert.False(
            transportConnection.ReceivedCancellationToken
                .IsCancellationRequested);

        // Act
        await coordinator.DisposeAsync();

        // Assert
        await transportConnection.ReceiveStopped;

        Assert.True(
            transportConnection.ReceivedCancellationToken
                .IsCancellationRequested);

        Assert.False(
            protocolConnection.Session.IsRunning);

        await coordinator.DisposeAsync();
    }

    private static RuntimeEndpoint CreateRuntimeEndpoint()
    {
        var descriptor =
            new EndpointDescriptor(
                new EndpointId(
                    "coordinator-duplex-endpoint"))
            {
                Metadata =
                    new EndpointMetadata
                    {
                        DisplayName =
                            "Coordinator Duplex Endpoint",
                        Description =
                            "Endpoint used to test coordinator-owned "
                            + "duplex-session lifecycle."
                    }
            };

        var context =
            new RuntimeContext();

        return context.AddEndpoint(
            descriptor);
    }

    private sealed class DualContractSynchronizer
        : IRuntimeEndpointSynchronizer,
          IRuntimeProtocolEndpointSynchronizer
    {
        public int TransportSynchronizationCount
        {
            get;
            private set;
        }

        public int ProtocolSynchronizationCount
        {
            get;
            private set;
        }

        public IRuntimeProtocolConnection?
            ReceivedProtocolConnection
        {
            get;
            private set;
        }

        public Task SynchronizeAsync(
            ITransportConnection connection,
            RuntimeEndpoint runtimeEndpoint,
            CancellationToken cancellationToken = default)
        {
            TransportSynchronizationCount++;

            throw new InvalidOperationException(
                "The transport synchronization contract should not "
                + "be selected.");
        }

        public Task SynchronizeAsync(
            IRuntimeProtocolConnection connection,
            RuntimeEndpoint runtimeEndpoint,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                connection);

            ArgumentNullException.ThrowIfNull(
                runtimeEndpoint);

            cancellationToken.ThrowIfCancellationRequested();

            ProtocolSynchronizationCount++;

            ReceivedProtocolConnection =
                connection;

            return Task.CompletedTask;
        }
    }

    private sealed class TestTransportFactory
        : ITransportFactory
    {
        private readonly ITransportConnection _connection;

        public TestTransportFactory(
            ITransportConnection connection)
        {
            _connection =
                connection
                ?? throw new ArgumentNullException(
                    nameof(connection));
        }

        public Task<ITransportConnection> ConnectAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                _connection);
        }
    }

    private sealed class TestDuplexTransportConnection
        : ITransportDuplexConnection
    {
        private readonly TaskCompletionSource _receiveStarted =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource _receiveStopped =
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

        public Task ReceiveStopped =>
            _receiveStopped.Task;

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
                "ExchangeAsync should not be used by a duplex coordinator.");
        }

        public Task SendAsync(
            byte[] request,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "No protocol request is expected by this lifecycle test.");
        }

        public async Task<byte[]> ReceiveAsync(
            CancellationToken cancellationToken = default)
        {
            ReceivedCancellationToken =
                cancellationToken;

            _receiveStarted.TrySetResult();

            try
            {
                await Task.Delay(
                    Timeout.InfiniteTimeSpan,
                    cancellationToken);
            }
            finally
            {
                _receiveStopped.TrySetResult();
            }

            throw new InvalidOperationException(
                "The cancelled receive unexpectedly continued.");
        }
    }
}