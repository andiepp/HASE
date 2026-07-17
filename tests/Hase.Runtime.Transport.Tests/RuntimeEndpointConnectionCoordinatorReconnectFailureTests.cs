using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class RuntimeEndpointConnectionCoordinatorReconnectFailureTests
{
    [Fact]
    public async Task ReconnectAsync_ReplacementFailure_ShouldBecomeFaulted()
    {
        var initialConnection =
            new TestTransportConnection();

        var expectedException =
            new IOException(
                "Replacement connection failed.");

        var factory =
            new TestTransportFactory();

        factory.EnqueueConnection(
            initialConnection);

        factory.EnqueueException(
            expectedException);

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

        await coordinator.ConnectAsync();

        initialConnection.TransitionTo(
            TransportConnectionState.Faulted);

        async Task Act()
        {
            await coordinator.ReconnectAsync();
        }

        IOException actualException =
            await Assert.ThrowsAsync<IOException>(
                Act);

        Assert.Same(
            expectedException,
            actualException);

        Assert.Equal(
            EndpointConnectionState.Faulted,
            runtimeEndpoint.ConnectionStatus.State);

        Assert.Equal(
            "The transport reconnection attempt failed.",
            runtimeEndpoint.ConnectionStatus.Detail);

        Assert.Same(
            initialConnection,
            connectionManager.CurrentConnection);

        Assert.Equal(
            TransportConnectionState.Faulted,
            connectionManager.CurrentState);

        Assert.Equal(
            2,
            factory.ConnectCallCount);

        Assert.Equal(
            0,
            connectionManager.ReplacementCount);

        Assert.Equal(
            1,
            synchronizer.SynchronizeCallCount);
    }

    [Fact]
    public async Task ReconnectAsync_SynchronizationFailure_ShouldBecomeFaulted()
    {
        var initialConnection =
            new TestTransportConnection();

        var replacementConnection =
            new TestTransportConnection();

        var factory =
            new TestTransportFactory();

        factory.EnqueueConnection(
            initialConnection);

        factory.EnqueueConnection(
            replacementConnection);

        await using var connectionManager =
            new TransportConnectionManager(
                factory);

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint();

        var expectedException =
            new InvalidDataException(
                "Reconnect synchronization failed.");

        var synchronizer =
            new TestRuntimeEndpointSynchronizer();

        synchronizer.EnqueueSuccess();

        synchronizer.EnqueueException(
            expectedException);

        await using var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                connectionManager,
                runtimeEndpoint,
                synchronizer);

        await coordinator.ConnectAsync();

        initialConnection.TransitionTo(
            TransportConnectionState.Faulted);

        async Task Act()
        {
            await coordinator.ReconnectAsync();
        }

        InvalidDataException actualException =
            await Assert.ThrowsAsync<InvalidDataException>(
                Act);

        Assert.Same(
            expectedException,
            actualException);

        Assert.Equal(
            EndpointConnectionState.Faulted,
            runtimeEndpoint.ConnectionStatus.State);

        Assert.Equal(
            "Endpoint synchronization failed.",
            runtimeEndpoint.ConnectionStatus.Detail);

        Assert.Same(
            replacementConnection,
            connectionManager.CurrentConnection);

        Assert.Equal(
            TransportConnectionState.Connected,
            connectionManager.CurrentState);

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
        private readonly Queue<
            Func<
                CancellationToken,
                Task<ITransportConnection>>> _results =
            new();

        public int ConnectCallCount
        {
            get;
            private set;
        }

        public void EnqueueConnection(
            ITransportConnection connection)
        {
            ArgumentNullException.ThrowIfNull(
                connection);

            _results.Enqueue(
                cancellationToken =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    return Task.FromResult(
                        connection);
                });
        }

        public void EnqueueException(
            Exception exception)
        {
            ArgumentNullException.ThrowIfNull(
                exception);

            _results.Enqueue(
                cancellationToken =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    return Task.FromException<ITransportConnection>(
                        exception);
                });
        }

        public Task<ITransportConnection> ConnectAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ConnectCallCount++;

            if (_results.Count == 0)
            {
                throw new InvalidOperationException(
                    "No test transport result is available.");
            }

            Func<
                CancellationToken,
                Task<ITransportConnection>> result =
                _results.Dequeue();

            return result(
                cancellationToken);
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

        public ITransportConnection? LastConnection
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

        public void EnqueueException(
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

            LastConnection =
                connection;

            if (_results.Count == 0)
            {
                return Task.CompletedTask;
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