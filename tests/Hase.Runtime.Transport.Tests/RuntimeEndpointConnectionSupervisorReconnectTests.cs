using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class RuntimeEndpointConnectionSupervisorReconnectTests
{
    [Fact]
    public async Task RunAsync_TransportFault_ShouldReconnectAndContinueSupervision()
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

        await synchronizer.FirstSynchronizationCompleted;

        Assert.Equal(
            EndpointConnectionState.Ready,
            runtimeEndpoint.ConnectionStatus.State);

        initialConnection.TransitionTo(
            TransportConnectionState.Faulted);

        await synchronizer.SecondSynchronizationCompleted;

        Assert.Equal(
            EndpointConnectionState.Ready,
            runtimeEndpoint.ConnectionStatus.State);

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
            new[] { 0 },
            reconnectPolicy.RetryAttempts);

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
        private readonly TaskCompletionSource<bool>
            _firstSynchronizationCompleted =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool>
            _secondSynchronizationCompleted =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        public int SynchronizeCallCount
        {
            get;
            private set;
        }

        public Task FirstSynchronizationCompleted =>
            _firstSynchronizationCompleted.Task;

        public Task SecondSynchronizationCompleted =>
            _secondSynchronizationCompleted.Task;

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

            if (SynchronizeCallCount == 1)
            {
                _firstSynchronizationCompleted.TrySetResult(
                    true);
            }
            else if (SynchronizeCallCount == 2)
            {
                _secondSynchronizationCompleted.TrySetResult(
                    true);
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