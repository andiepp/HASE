using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class
    RuntimeEndpointConnectionCoordinatorDuplexReuseTests
{
    [Fact]
    public async Task ReconnectAsync_ConnectedTransport_ShouldReuseDuplexSession()
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
            new FailOnceProtocolSynchronizer();

        DuplexRuntimeProtocolConnection firstProtocolConnection;

        await using (
            var coordinator =
                new RuntimeEndpointConnectionCoordinator(
                    connectionManager,
                    runtimeEndpoint,
                    synchronizer))
        {
            // Act
            await Assert.ThrowsAsync<InvalidDataException>(
                async () => await coordinator.ConnectAsync());

            await transportConnection.ReceiveStarted;

            // Assert
            Assert.Equal(
                EndpointConnectionState.Faulted,
                runtimeEndpoint.ConnectionStatus.State);

            Assert.Equal(
                TransportConnectionState.Connected,
                transportConnection.State);

            Assert.Equal(
                1,
                synchronizer.ProtocolConnections.Count);

            firstProtocolConnection =
                Assert.IsType<DuplexRuntimeProtocolConnection>(
                    synchronizer.ProtocolConnections[0]);

            Assert.True(
                firstProtocolConnection.Session.IsRunning);

            // Act
            ITransportConnection recoveredConnection =
                await coordinator.ReconnectAsync();

            // Assert
            Assert.Same(
                transportConnection,
                recoveredConnection);

            Assert.Equal(
                EndpointConnectionState.Ready,
                runtimeEndpoint.ConnectionStatus.State);

            Assert.Equal(
                2,
                synchronizer.ProtocolConnections.Count);

            Assert.Same(
                firstProtocolConnection,
                synchronizer.ProtocolConnections[1]);

            Assert.True(
                firstProtocolConnection.Session.IsRunning);

            Assert.Equal(
                1,
                transportConnection.ReceiveCallCount);

            Assert.Equal(
                1,
                transportFactory.ConnectCallCount);

            Assert.Equal(
                0,
                connectionManager.ReplacementCount);
        }

        await transportConnection.ReceiveStopped;

        Assert.True(
            transportConnection.ReceivedCancellationToken
                .IsCancellationRequested);

        Assert.False(
            firstProtocolConnection.Session.IsRunning);
    }

    private static RuntimeEndpoint CreateRuntimeEndpoint()
    {
        var descriptor =
            new EndpointDescriptor(
                new EndpointId(
                    "coordinator-duplex-reuse-endpoint"))
            {
                Metadata =
                    new EndpointMetadata
                    {
                        DisplayName =
                            "Coordinator Duplex Reuse Endpoint",
                        Description =
                            "Endpoint used to verify duplex-session reuse "
                            + "after synchronization failure."
                    }
            };

        var context =
            new RuntimeContext();

        return context.AddEndpoint(
            descriptor);
    }

    private sealed class FailOnceProtocolSynchronizer
        : IRuntimeEndpointSynchronizer,
          IRuntimeProtocolEndpointSynchronizer
    {
        private readonly List<IRuntimeProtocolConnection>
            _protocolConnections =
                new();

        public IReadOnlyList<IRuntimeProtocolConnection>
            ProtocolConnections =>
                _protocolConnections;

        public Task SynchronizeAsync(
            ITransportConnection connection,
            RuntimeEndpoint runtimeEndpoint,
            CancellationToken cancellationToken = default)
        {
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

            _protocolConnections.Add(
                connection);

            if (_protocolConnections.Count == 1)
            {
                throw new InvalidDataException(
                    "The first synchronization attempt failed.");
            }

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

        public int ConnectCallCount
        {
            get;
            private set;
        }

        public Task<ITransportConnection> ConnectAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ConnectCallCount++;

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

        public int ReceiveCallCount
        {
            get;
            private set;
        }

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
                "No protocol request is expected by this reuse test.");
        }

        public async Task<byte[]> ReceiveAsync(
            CancellationToken cancellationToken = default)
        {
            ReceiveCallCount++;

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