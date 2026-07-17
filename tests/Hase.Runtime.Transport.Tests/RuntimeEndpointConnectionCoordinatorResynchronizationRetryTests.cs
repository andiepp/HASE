using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class RuntimeEndpointConnectionCoordinatorResynchronizationRetryTests
{
    [Fact]
    public async Task ReconnectAsync_AfterSynchronizationFailure_ShouldReuseConnectedTransport()
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

        synchronizer.EnqueueSuccess();
        synchronizer.EnqueueFailure(
            new InvalidDataException(
                "First reconnect synchronization failed."));
        synchronizer.EnqueueSuccess();

        await using var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                connectionManager,
                runtimeEndpoint,
                synchronizer);

        await coordinator.ConnectAsync();

        initialConnection.TransitionTo(
            TransportConnectionState.Faulted);

        await Assert.ThrowsAsync<InvalidDataException>(
            async () => await coordinator.ReconnectAsync());

        Assert.Equal(
            EndpointConnectionState.Faulted,
            runtimeEndpoint.ConnectionStatus.State);

        Assert.Same(
            replacementConnection,
            connectionManager.CurrentConnection);

        Assert.Equal(
            TransportConnectionState.Connected,
            connectionManager.CurrentState);

        ITransportConnection recoveredConnection =
            await coordinator.ReconnectAsync();

        Assert.Same(
            replacementConnection,
            recoveredConnection);

        Assert.Same(
            replacementConnection,
            connectionManager.CurrentConnection);

        Assert.Equal(
            EndpointConnectionState.Ready,
            runtimeEndpoint.ConnectionStatus.State);

        Assert.Equal(
            2,
            factory.ConnectCallCount);

        Assert.Equal(
            1,
            connectionManager.ReplacementCount);

        Assert.Equal(
            3,
            synchronizer.SynchronizeCallCount);

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
        private readonly Queue<
            Func<CancellationToken, Task>> _results =
            new();

        public int SynchronizeCallCount
        {
            get;
            private set;
        }

        public void EnqueueSuccess()
        {
            _results.Enqueue(
                cancellationToken =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    return Task.CompletedTask;
                });
        }

        public void EnqueueFailure(
            Exception exception)
        {
            ArgumentNullException.ThrowIfNull(
                exception);

            _results.Enqueue(
                cancellationToken =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    return Task.FromException(
                        exception);
                });
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

            if (_results.Count == 0)
            {
                throw new InvalidOperationException(
                    "No synchronization result is available.");
            }

            Func<CancellationToken, Task> result =
                _results.Dequeue();

            return result(
                cancellationToken);
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