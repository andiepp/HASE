using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class RuntimeEndpointConnectionSupervisorInitialStatisticsTests
{
    [Fact]
    public async Task GetStatistics_InitialRetries_ShouldCountAttemptsAndFailures()
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
                "First retry failed."));

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

        var supervisor =
            new RuntimeEndpointConnectionSupervisor(
                coordinator,
                new ImmediateReconnectPolicy());

        RuntimeEndpointConnectionStatistics beforeStart =
            supervisor.GetStatistics();

        Assert.Equal(
            RuntimeEndpointConnectionStatistics.Empty,
            beforeStart);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task supervisionTask =
            supervisor.RunAsync(
                cancellationTokenSource.Token);

        await synchronizer.SynchronizationCompleted;

        RuntimeEndpointConnectionStatistics afterConnection =
            supervisor.GetStatistics();

        Assert.Equal(
            3,
            afterConnection.InitialConnectionAttemptCount);

        Assert.Equal(
            2,
            afterConnection.InitialConnectionFailureCount);

        Assert.Equal(
            0,
            afterConnection.ReconnectAttemptCount);

        Assert.Equal(
            0,
            afterConnection.ReconnectFailureCount);

        Assert.Equal(
            0,
            afterConnection.SuccessfulRecoveryCount);

        Assert.Null(
            afterConnection.LastRecoveryStartedAtUtc);

        Assert.Null(
            afterConnection.LastRecoveryCompletedAtUtc);

        Assert.Null(
            afterConnection.LastRecoveryDuration);

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await supervisionTask);

        RuntimeEndpointConnectionStatistics afterCancellation =
            supervisor.GetStatistics();

        Assert.Equal(
            3,
            afterCancellation.InitialConnectionAttemptCount);

        Assert.Equal(
            2,
            afterCancellation.InitialConnectionFailureCount);
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

    private sealed class ImmediateReconnectPolicy
        : IRuntimeEndpointReconnectPolicy
    {
        public TimeSpan GetDelay(
            int retryAttempt)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(
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