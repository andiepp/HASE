using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class RuntimeEndpointConnectionCoordinatorReconnectCancellationTests
{
    [Fact]
    public async Task ReconnectAsync_ReplacementCancelled_ShouldBecomeDisconnected()
    {
        var initialConnection =
            new TestTransportConnection();

        var factory =
            new TestTransportFactory();

        factory.EnqueueConnection(
            initialConnection);

        factory.EnqueueCancellation();

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

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task<ITransportConnection> reconnectTask =
            coordinator.ReconnectAsync(
                cancellationTokenSource.Token);

        await factory.SecondConnectStarted;

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await reconnectTask);

        Assert.Equal(
            EndpointConnectionState.Disconnected,
            runtimeEndpoint.ConnectionStatus.State);

        Assert.Equal(
            "The transport reconnection attempt was cancelled.",
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
    public async Task ReconnectAsync_SynchronizationCancelled_ShouldBecomeDisconnected()
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

        var synchronizer =
            new TestRuntimeEndpointSynchronizer();

        synchronizer.EnqueueSuccess();
        synchronizer.EnqueueCancellation();

        await using var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                connectionManager,
                runtimeEndpoint,
                synchronizer);

        await coordinator.ConnectAsync();

        initialConnection.TransitionTo(
            TransportConnectionState.Faulted);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task<ITransportConnection> reconnectTask =
            coordinator.ReconnectAsync(
                cancellationTokenSource.Token);

        await synchronizer.SecondSynchronizationStarted;

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await reconnectTask);

        Assert.Equal(
            EndpointConnectionState.Disconnected,
            runtimeEndpoint.ConnectionStatus.State);

        Assert.Equal(
            "Endpoint synchronization was cancelled.",
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

        private readonly TaskCompletionSource<bool>
            _secondConnectStarted =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        public int ConnectCallCount
        {
            get;
            private set;
        }

        public Task SecondConnectStarted =>
            _secondConnectStarted.Task;

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

        public void EnqueueCancellation()
        {
            _results.Enqueue(
                async cancellationToken =>
                {
                    await Task.Delay(
                        Timeout.InfiniteTimeSpan,
                        cancellationToken);

                    throw new InvalidOperationException(
                        "The cancellation wait completed unexpectedly.");
                });
        }

        public Task<ITransportConnection> ConnectAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ConnectCallCount++;

            if (ConnectCallCount == 2)
            {
                _secondConnectStarted.TrySetResult(
                    true);
            }

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

        private readonly TaskCompletionSource<bool>
            _secondSynchronizationStarted =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        public int SynchronizeCallCount
        {
            get;
            private set;
        }

        public Task SecondSynchronizationStarted =>
            _secondSynchronizationStarted.Task;

        public void EnqueueSuccess()
        {
            _results.Enqueue(
                cancellationToken =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    return Task.CompletedTask;
                });
        }

        public void EnqueueCancellation()
        {
            _results.Enqueue(
                async cancellationToken =>
                {
                    await Task.Delay(
                        Timeout.InfiniteTimeSpan,
                        cancellationToken);
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

            if (SynchronizeCallCount == 2)
            {
                _secondSynchronizationStarted.TrySetResult(
                    true);
            }

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