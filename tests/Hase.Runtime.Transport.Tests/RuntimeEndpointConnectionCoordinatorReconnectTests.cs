using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class RuntimeEndpointConnectionCoordinatorReconnectTests
{
    [Fact]
    public async Task ReconnectAsync_FaultedConnection_ShouldReplaceAndSynchronize()
    {
        var initialConnection =
            new TestTransportConnection();

        var replacementConnection =
            new TestTransportConnection();

        var factory =
            new TestTransportFactory(
                initialConnection,
                replacementConnection);

        await using var connectionManager =
            new TransportConnectionManager(
                factory);

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint();

        var synchronizer =
            new TestRuntimeEndpointSynchronizer();

        await using var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                connectionManager,
                runtimeEndpoint,
                synchronizer);

        ITransportConnection connected =
            await coordinator.ConnectAsync();

        Assert.Same(
            initialConnection,
            connected);

        Assert.Equal(
            EndpointConnectionState.Ready,
            runtimeEndpoint.ConnectionStatus.State);

        initialConnection.TransitionTo(
            TransportConnectionState.Faulted);

        Assert.Equal(
            EndpointConnectionState.Faulted,
            runtimeEndpoint.ConnectionStatus.State);

        ITransportConnection reconnected =
            await coordinator.ReconnectAsync();

        Assert.Same(
            replacementConnection,
            reconnected);

        Assert.Same(
            replacementConnection,
            connectionManager.CurrentConnection);

        Assert.Equal(
            TransportConnectionState.Connected,
            connectionManager.CurrentState);

        Assert.Equal(
            EndpointConnectionState.Ready,
            runtimeEndpoint.ConnectionStatus.State);

        Assert.Equal(
            "The endpoint is connected, synchronized, and ready.",
            runtimeEndpoint.ConnectionStatus.Detail);

        Assert.Equal(
            2,
            factory.ConnectCallCount);

        Assert.Equal(
            1,
            connectionManager.ReplacementCount);

        Assert.Equal(
            2,
            synchronizer.SynchronizeCallCount);

        Assert.Same(
            replacementConnection,
            synchronizer.LastConnection);

        Assert.Same(
            runtimeEndpoint,
            synchronizer.LastRuntimeEndpoint);

        Assert.Equal(
            1,
            initialConnection.DisposeCallCount);
    }

    private static RuntimeEndpoint CreateRuntimeEndpoint()
    {
        var context =
            new RuntimeContext();

        return context.AddEndpoint(
            new EndpointDescriptor(
                new EndpointId(
                    "Endpoint")));
    }

    private sealed class TestTransportFactory
        : ITransportFactory
    {
        private readonly Queue<ITransportConnection> _connections;

        public TestTransportFactory(
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

            if (_connections.Count == 0)
            {
                throw new InvalidOperationException(
                    "No test transport connection is available.");
            }

            return Task.FromResult(
                _connections.Dequeue());
        }
    }

    private sealed class TestRuntimeEndpointSynchronizer
        : IRuntimeEndpointSynchronizer
    {
        public int SynchronizeCallCount
        {
            get;
            private set;
        }

        public ITransportConnection? LastConnection
        {
            get;
            private set;
        }

        public RuntimeEndpoint? LastRuntimeEndpoint
        {
            get;
            private set;
        }

        public Task SynchronizeAsync(
            ITransportConnection connection,
            RuntimeEndpoint runtimeEndpoint,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                connection);

            ArgumentNullException.ThrowIfNull(
                runtimeEndpoint);

            cancellationToken.ThrowIfCancellationRequested();

            SynchronizeCallCount++;

            LastConnection =
                connection;

            LastRuntimeEndpoint =
                runtimeEndpoint;

            return Task.CompletedTask;
        }
    }

    private sealed class TestTransportConnection
        : ITransportConnection,
          IAsyncDisposable
    {
        private TransportConnectionState _state =
            TransportConnectionState.Connected;

        public event EventHandler<
            TransportConnectionStateChangedEventArgs>?
            StateChanged;

        public TransportConnectionState State =>
            _state;

        public int DisposeCallCount
        {
            get;
            private set;
        }

        public Task<byte[]> ExchangeAsync(
            byte[] request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                request);

            cancellationToken.ThrowIfCancellationRequested();

            if (_state != TransportConnectionState.Connected)
            {
                throw new InvalidOperationException(
                    "The test transport connection is not connected.");
            }

            return Task.FromResult(
                request);
        }

        public ValueTask DisposeAsync()
        {
            DisposeCallCount++;

            TransitionTo(
                TransportConnectionState.Closed);

            return ValueTask.CompletedTask;
        }

        public void TransitionTo(
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