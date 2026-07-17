using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class RuntimeEndpointConnectionSupervisorInitialRetryTests
{
    [Fact]
    public async Task RunAsync_InitialConnectionFailures_ShouldRetryUntilReady()
    {
        var connection =
            new TestTransportConnection();

        var factory =
            new TestTransportFactory();

        factory.EnqueueFailure(
            new IOException(
                "Initial connection failed."));

        factory.EnqueueFailure(
            new IOException(
                "First connection retry failed."));

        factory.EnqueueConnection(
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

        await synchronizer.SynchronizationCompleted;

        Assert.Equal(
            EndpointConnectionState.Ready,
            runtimeEndpoint.ConnectionStatus.State);

        Assert.Same(
            connection,
            connectionManager.CurrentConnection);

        Assert.Equal(
            3,
            factory.ConnectCallCount);

        Assert.Equal(
            new[] { 0, 1 },
            reconnectPolicy.RetryAttempts);

        Assert.Equal(
            1,
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
        public event EventHandler<
            TransportConnectionStateChangedEventArgs>?
            StateChanged;

        public TransportConnectionState State =>
            TransportConnectionState.Connected;

        public Task<byte[]> ExchangeAsync(
            byte[] request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                request);

            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                request);
        }
    }
}