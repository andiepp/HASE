using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class RuntimeEndpointConnectionSupervisorRetryTests
{
    [Fact]
    public async Task RunAsync_ReconnectFailures_ShouldRetryAndResetAttemptAfterSuccess()
    {
        var initialConnection =
            new TestTransportConnection();

        var firstReplacementConnection =
            new TestTransportConnection();

        var secondReplacementConnection =
            new TestTransportConnection();

        var factory =
            new TestTransportFactory();

        factory.EnqueueConnection(
            initialConnection);

        factory.EnqueueFailure(
            new IOException(
                "First reconnect attempt failed."));

        factory.EnqueueFailure(
            new IOException(
                "Second reconnect attempt failed."));

        factory.EnqueueConnection(
            firstReplacementConnection);

        factory.EnqueueConnection(
            secondReplacementConnection);

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

        var reconnectPolicy =
            new TestReconnectPolicy();

        var supervisor =
            new RuntimeEndpointConnectionSupervisor(
                coordinator,
                reconnectPolicy);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task supervisionTask =
            supervisor.RunAsync(
                cancellationTokenSource.Token);

        await synchronizer.InitialSynchronizationCompleted;

        initialConnection.TransitionTo(
            TransportConnectionState.Faulted);

        await synchronizer.FirstRecoverySynchronizationCompleted;

        Assert.Equal(
            EndpointConnectionState.Ready,
            runtimeEndpoint.ConnectionStatus.State);

        Assert.Same(
            firstReplacementConnection,
            connectionManager.CurrentConnection);

        Assert.Equal(
            new[] { 0, 1, 2 },
            reconnectPolicy.RetryAttempts);

        Assert.Equal(
            4,
            factory.ConnectCallCount);

        Assert.Equal(
            1,
            connectionManager.ReplacementCount);

        firstReplacementConnection.TransitionTo(
            TransportConnectionState.Faulted);

        await synchronizer.SecondRecoverySynchronizationCompleted;

        Assert.Equal(
            EndpointConnectionState.Ready,
            runtimeEndpoint.ConnectionStatus.State);

        Assert.Same(
            secondReplacementConnection,
            connectionManager.CurrentConnection);

        Assert.Equal(
            new[] { 0, 1, 2, 0 },
            reconnectPolicy.RetryAttempts);

        Assert.Equal(
            5,
            factory.ConnectCallCount);

        Assert.Equal(
            2,
            connectionManager.ReplacementCount);

        Assert.Equal(
            3,
            synchronizer.SynchronizeCallCount);

        Assert.False(
            supervisionTask.IsCompleted);

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await supervisionTask);

        Assert.Equal(
            EndpointConnectionState.Disconnected,
            runtimeEndpoint.ConnectionStatus.State);
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

    private sealed class TestReconnectPolicy
        : IRuntimeEndpointReconnectPolicy
    {
        public List<int> RetryAttempts
        {
            get;
        } = [];

        public TimeSpan GetDelay(
            int retryAttempt)
        {
            RetryAttempts.Add(
                retryAttempt);

            return TimeSpan.Zero;
        }
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

        public void EnqueueFailure(
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
        private readonly TaskCompletionSource<bool>
            _initialSynchronizationCompleted =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool>
            _firstRecoverySynchronizationCompleted =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool>
            _secondRecoverySynchronizationCompleted =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        public int SynchronizeCallCount
        {
            get;
            private set;
        }

        public Task InitialSynchronizationCompleted =>
            _initialSynchronizationCompleted.Task;

        public Task FirstRecoverySynchronizationCompleted =>
            _firstRecoverySynchronizationCompleted.Task;

        public Task SecondRecoverySynchronizationCompleted =>
            _secondRecoverySynchronizationCompleted.Task;

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

            switch (SynchronizeCallCount)
            {
                case 1:
                    _initialSynchronizationCompleted.TrySetResult(
                        true);
                    break;

                case 2:
                    _firstRecoverySynchronizationCompleted.TrySetResult(
                        true);
                    break;

                case 3:
                    _secondRecoverySynchronizationCompleted.TrySetResult(
                        true);
                    break;
            }

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