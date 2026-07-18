using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class
    RuntimeEndpointConnectionCoordinatorDuplexReplacementTests
{
    [Fact]
    public async Task ReconnectAsync_FaultedDuplexTransport_ShouldReplaceBinding()
    {
        // Arrange
        var initialConnection =
            new TestDuplexTransportConnection();

        var replacementConnection =
            new TestDuplexTransportConnection();

        var transportFactory =
            new QueuedTransportFactory(
                initialConnection,
                replacementConnection);

        await using var connectionManager =
            new TransportConnectionManager(
                transportFactory);

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint();

        var synchronizer =
            new RecordingProtocolSynchronizer();

        DuplexRuntimeProtocolConnection initialProtocolConnection;
        DuplexRuntimeProtocolConnection replacementProtocolConnection;

        await using (
            var coordinator =
                new RuntimeEndpointConnectionCoordinator(
                    connectionManager,
                    runtimeEndpoint,
                    synchronizer))
        {
            // Act
            ITransportConnection connectedTransport =
                await coordinator.ConnectAsync();

            await initialConnection.ReceiveStarted;

            // Assert
            Assert.Same(
                initialConnection,
                connectedTransport);

            initialProtocolConnection =
                Assert.IsType<DuplexRuntimeProtocolConnection>(
                    Assert.Single(
                        synchronizer.ProtocolConnections));

            Assert.True(
                initialProtocolConnection.Session.IsRunning);

            Assert.Equal(
                EndpointConnectionState.Ready,
                runtimeEndpoint.ConnectionStatus.State);

            // Act
            initialConnection.Fault(
                new IOException(
                    "The simulated duplex receive failed."));

            await initialConnection.ReceiveStopped;

            // Assert
            Assert.Equal(
                TransportConnectionState.Faulted,
                initialConnection.State);

            Assert.Equal(
                EndpointConnectionState.Faulted,
                runtimeEndpoint.ConnectionStatus.State);

            // Act
            ITransportConnection recoveredTransport =
                await coordinator.ReconnectAsync();

            await replacementConnection.ReceiveStarted;

            // Assert
            Assert.Same(
                replacementConnection,
                recoveredTransport);

            Assert.Same(
                replacementConnection,
                connectionManager.CurrentConnection);

            Assert.Equal(
                1,
                connectionManager.ReplacementCount);

            Assert.Equal(
                2,
                transportFactory.ConnectCallCount);

            Assert.Equal(
                1,
                initialConnection.DisposeCallCount);

            Assert.Equal(
                2,
                synchronizer.ProtocolConnections.Count);

            replacementProtocolConnection =
                Assert.IsType<DuplexRuntimeProtocolConnection>(
                    synchronizer.ProtocolConnections[1]);

            Assert.NotSame(
                initialProtocolConnection,
                replacementProtocolConnection);

            Assert.NotSame(
                initialProtocolConnection.Session,
                replacementProtocolConnection.Session);

            Assert.False(
                initialProtocolConnection.Session.IsRunning);

            Assert.True(
                replacementProtocolConnection.Session.IsRunning);

            Assert.Equal(
                1,
                initialConnection.ReceiveCallCount);

            Assert.Equal(
                1,
                replacementConnection.ReceiveCallCount);

            Assert.Equal(
                EndpointConnectionState.Ready,
                runtimeEndpoint.ConnectionStatus.State);
        }

        await replacementConnection.ReceiveStopped;

        Assert.True(
            replacementConnection.ReceivedCancellationToken
                .IsCancellationRequested);

        Assert.False(
            replacementProtocolConnection.Session.IsRunning);
    }

    private static RuntimeEndpoint CreateRuntimeEndpoint()
    {
        var descriptor =
            new EndpointDescriptor(
                new EndpointId(
                    "coordinator-duplex-replacement-endpoint"))
            {
                Metadata =
                    new EndpointMetadata
                    {
                        DisplayName =
                            "Coordinator Duplex Replacement Endpoint",
                        Description =
                            "Endpoint used to verify duplex binding "
                            + "replacement after transport failure."
                    }
            };

        var context =
            new RuntimeContext();

        return context.AddEndpoint(
            descriptor);
    }

    private sealed class RecordingProtocolSynchronizer
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

            return Task.CompletedTask;
        }
    }

    private sealed class QueuedTransportFactory
        : ITransportFactory
    {
        private readonly Queue<ITransportConnection>
            _connections;

        public QueuedTransportFactory(
            params ITransportConnection[] connections)
        {
            ArgumentNullException.ThrowIfNull(
                connections);

            _connections =
                new Queue<ITransportConnection>(
                    connections);
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

            if (!_connections.TryDequeue(
                    out ITransportConnection? connection))
            {
                throw new InvalidOperationException(
                    "No test transport connection remains.");
            }

            return Task.FromResult(
                connection);
        }
    }

    private sealed class TestDuplexTransportConnection
        : ITransportDuplexConnection,
          IAsyncDisposable
    {
        private readonly TaskCompletionSource<byte[]>
            _receiveCompletion =
                new(
                    TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource _receiveStarted =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource _receiveStopped =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        private TransportConnectionState _state =
            TransportConnectionState.Connected;

        public event EventHandler<
            TransportConnectionStateChangedEventArgs>?
            StateChanged;

        public TransportConnectionState State =>
            _state;

        public Task ReceiveStarted =>
            _receiveStarted.Task;

        public Task ReceiveStopped =>
            _receiveStopped.Task;

        public int ReceiveCallCount
        {
            get;
            private set;
        }

        public int DisposeCallCount
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
                "No protocol request is expected by this replacement test.");
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
                return await _receiveCompletion.Task.WaitAsync(
                    cancellationToken);
            }
            finally
            {
                _receiveStopped.TrySetResult();
            }
        }

        public void Fault(
            Exception exception)
        {
            ArgumentNullException.ThrowIfNull(
                exception);

            TransitionTo(
                TransportConnectionState.Faulted);

            _receiveCompletion.TrySetException(
                exception);
        }

        public ValueTask DisposeAsync()
        {
            DisposeCallCount++;

            TransitionTo(
                TransportConnectionState.Closed);

            return ValueTask.CompletedTask;
        }

        private void TransitionTo(
            TransportConnectionState state)
        {
            TransportConnectionState previousState =
                _state;

            if (previousState == state)
            {
                return;
            }

            _state =
                state;

            StateChanged?.Invoke(
                this,
                new TransportConnectionStateChangedEventArgs(
                    previousState,
                    state));
        }
    }
}