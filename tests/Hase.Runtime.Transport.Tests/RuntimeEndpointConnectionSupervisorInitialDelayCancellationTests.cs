using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class RuntimeEndpointConnectionSupervisorInitialDelayCancellationTests
{
    [Fact]
    public async Task RunAsync_CancelledDuringInitialRetryDelay_ShouldBecomeDisconnected()
    {
        var factory =
            new TestTransportFactory();

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

        await reconnectPolicy.DelayRequested;

        Assert.Equal(
            EndpointConnectionState.Faulted,
            runtimeEndpoint.ConnectionStatus.State);

        Assert.Equal(
            1,
            factory.ConnectCallCount);

        Assert.Equal(
            0,
            synchronizer.SynchronizeCallCount);

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

        Assert.Null(
            connectionManager.CurrentConnection);

        Assert.Equal(
            0,
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

            return Task.FromException<ITransportConnection>(
                new IOException(
                    "The endpoint is unavailable."));
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

            return Task.CompletedTask;
        }
    }
}