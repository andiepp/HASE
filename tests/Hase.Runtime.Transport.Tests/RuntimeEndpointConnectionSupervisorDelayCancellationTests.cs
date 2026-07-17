using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class RuntimeEndpointConnectionSupervisorDelayCancellationTests
{
    [Fact]
    public async Task RunAsync_CancelledDuringReconnectDelay_ShouldBecomeDisconnected()
    {
        var connection =
            new TestTransportConnection();

        var factory =
            new TestTransportFactory(
                connection);

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
            new TestReconnectPolicy(
                TimeSpan.FromHours(1));

        var supervisor =
            new RuntimeEndpointConnectionSupervisor(
                coordinator,
                reconnectPolicy);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task supervisionTask =
            supervisor.RunAsync(
                cancellationTokenSource.Token);

        await synchronizer.SynchronizationCompleted;

        Assert.Equal(
            EndpointConnectionState.Ready,
            runtimeEndpoint.ConnectionStatus.State);

        connection.TransitionTo(
            TransportConnectionState.Faulted);

        await reconnectPolicy.DelayRequested;

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await supervisionTask);

        Assert.Equal(
            EndpointConnectionState.Disconnected,
            runtimeEndpoint.ConnectionStatus.State);

        Assert.Equal(
            "Endpoint connection supervision was cancelled.",
            runtimeEndpoint.ConnectionStatus.Detail);

        Assert.Equal(
            new[] { 0 },
            reconnectPolicy.RetryAttempts);

        Assert.Equal(
            1,
            factory.ConnectCallCount);

        Assert.Equal(
            0,
            connectionManager.ReplacementCount);

        Assert.Equal(
            1,
            synchronizer.SynchronizeCallCount);
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
        private readonly TimeSpan _delay;

        private readonly TaskCompletionSource<bool>
            _delayRequested =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        public TestReconnectPolicy(
            TimeSpan delay)
        {
            if (delay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(delay));
            }

            _delay =
                delay;
        }

        public List<int> RetryAttempts
        {
            get;
        } = [];

        public Task DelayRequested =>
            _delayRequested.Task;

        public TimeSpan GetDelay(
            int retryAttempt)
        {
            RetryAttempts.Add(
                retryAttempt);

            _delayRequested.TrySetResult(
                true);

            return _delay;
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

    private sealed class TestRuntimeEndpointSynchronizer
        : IRuntimeEndpointSynchronizer
    {
        private readonly TaskCompletionSource<bool>
            _synchronizationCompleted =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        public int SynchronizeCallCount
        {
            get;
            private set;
        }

        public Task SynchronizationCompleted =>
            _synchronizationCompleted.Task;

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

            _synchronizationCompleted.TrySetResult(
                true);

            return Task.CompletedTask;
        }
    }

    private sealed class TestTransportConnection
        : ITransportConnection
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