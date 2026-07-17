using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class RuntimeEndpointConnectionSupervisorRecoveryTimingCancellationTests
{
    [Fact]
    public async Task GetStatistics_CancelledRecovery_ShouldRetainStartWithoutCompletion()
    {
        DateTimeOffset recoveryStartedAtUtc =
            DateTimeOffset.FromUnixTimeMilliseconds(
                1_750_000_000_000);

        var timeProvider =
            new TestTimeProvider(
                recoveryStartedAtUtc);

        var initialConnection =
            new TestTransportConnection();

        var factory =
            new TestTransportFactory(
                initialConnection);

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
                new ImmediateReconnectPolicy(),
                timeProvider);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task supervisionTask =
            supervisor.RunAsync(
                cancellationTokenSource.Token);

        await synchronizer.InitialSynchronizationCompleted;

        initialConnection.TransitionTo(
            TransportConnectionState.Faulted);

        await factory.RecoveryConnectStarted;

        RuntimeEndpointConnectionStatistics duringRecovery =
            await WaitForRecoveryAttemptAsync(
                supervisor);

        Assert.Equal(
            recoveryStartedAtUtc,
            duringRecovery.LastRecoveryStartedAtUtc);

        Assert.Null(
            duringRecovery.LastRecoveryCompletedAtUtc);

        Assert.Null(
            duringRecovery.LastRecoveryDuration);

        Assert.Equal(
            1,
            duringRecovery.ReconnectAttemptCount);

        Assert.Equal(
            0,
            duringRecovery.ReconnectFailureCount);

        Assert.Equal(
            0,
            duringRecovery.SuccessfulRecoveryCount);

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await supervisionTask);

        RuntimeEndpointConnectionStatistics afterCancellation =
            supervisor.GetStatistics();

        Assert.Equal(
            recoveryStartedAtUtc,
            afterCancellation.LastRecoveryStartedAtUtc);

        Assert.Null(
            afterCancellation.LastRecoveryCompletedAtUtc);

        Assert.Null(
            afterCancellation.LastRecoveryDuration);

        Assert.Equal(
            1,
            afterCancellation.ReconnectAttemptCount);

        Assert.Equal(
            0,
            afterCancellation.ReconnectFailureCount);

        Assert.Equal(
            0,
            afterCancellation.SuccessfulRecoveryCount);

        Assert.Equal(
            EndpointConnectionState.Disconnected,
            runtimeEndpoint.ConnectionStatus.State);
    }

    private static async Task<RuntimeEndpointConnectionStatistics>
        WaitForRecoveryAttemptAsync(
            RuntimeEndpointConnectionSupervisor supervisor)
    {
        ArgumentNullException.ThrowIfNull(
            supervisor);

        using var timeoutCancellationTokenSource =
            new CancellationTokenSource(
                TimeSpan.FromSeconds(
                    5));

        while (true)
        {
            timeoutCancellationTokenSource.Token
                .ThrowIfCancellationRequested();

            RuntimeEndpointConnectionStatistics statistics =
                supervisor.GetStatistics();

            if (statistics.ReconnectAttemptCount == 1)
            {
                return statistics;
            }

            await Task.Yield();
        }
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

    private sealed class TestTimeProvider
        : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public TestTimeProvider(
            DateTimeOffset utcNow)
        {
            if (utcNow.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "The initial time must be expressed in UTC.",
                    nameof(utcNow));
            }

            _utcNow =
                utcNow;
        }

        public override long TimestampFrequency =>
            TimeSpan.TicksPerSecond;

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }

        public override long GetTimestamp()
        {
            return 0;
        }
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
        private readonly ITransportConnection _initialConnection;

        private readonly TaskCompletionSource<bool>
            _recoveryConnectStarted =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        private int _connectCallCount;

        public TestTransportFactory(
            ITransportConnection initialConnection)
        {
            _initialConnection =
                initialConnection
                ?? throw new ArgumentNullException(
                    nameof(initialConnection));
        }

        public Task RecoveryConnectStarted =>
            _recoveryConnectStarted.Task;

        public async Task<ITransportConnection> ConnectAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _connectCallCount++;

            if (_connectCallCount == 1)
            {
                return _initialConnection;
            }

            if (_connectCallCount == 2)
            {
                _recoveryConnectStarted.TrySetResult(
                    true);

                await Task.Delay(
                    Timeout.InfiniteTimeSpan,
                    cancellationToken);

                throw new InvalidOperationException(
                    "The recovery wait completed unexpectedly.");
            }

            throw new InvalidOperationException(
                "No additional transport result is available.");
        }
    }

    private sealed class TestRuntimeEndpointSynchronizer
        : IRuntimeEndpointSynchronizer
    {
        private readonly TaskCompletionSource<bool>
            _initialSynchronizationCompleted =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        public Task InitialSynchronizationCompleted =>
            _initialSynchronizationCompleted.Task;

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

            _initialSynchronizationCompleted.TrySetResult(
                true);

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