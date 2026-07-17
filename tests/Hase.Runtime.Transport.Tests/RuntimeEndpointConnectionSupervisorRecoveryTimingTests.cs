using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class RuntimeEndpointConnectionSupervisorRecoveryTimingTests
{
    [Fact]
    public async Task GetStatistics_RecoveryWithFailure_ShouldRecordTotalTiming()
    {
        var timeProvider =
            new TestTimeProvider(
                DateTimeOffset.FromUnixTimeMilliseconds(
                    1_750_000_000_000));

        var initialConnection =
            new TestTransportConnection();

        var replacementConnection =
            new TestTransportConnection();

        var factory =
            new TestTransportFactory();

        factory.EnqueueConnection(
            initialConnection);

        factory.EnqueueFailure(
            new IOException(
                "First recovery attempt failed."),
            () => timeProvider.Advance(
                TimeSpan.FromSeconds(
                    2)));

        factory.EnqueueConnection(
            replacementConnection,
            () => timeProvider.Advance(
                TimeSpan.FromSeconds(
                    3)));

        await using var connectionManager =
            new TransportConnectionManager(
                factory);

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint();

        var synchronizer =
            new TestRuntimeEndpointSynchronizer(
                () => timeProvider.Advance(
                    TimeSpan.FromSeconds(
                        1)));

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

        DateTimeOffset expectedStartedAtUtc =
            timeProvider.GetUtcNow();

        initialConnection.TransitionTo(
            TransportConnectionState.Faulted);

        await synchronizer.RecoverySynchronizationCompleted;

        RuntimeEndpointConnectionStatistics statistics =
            await WaitForSuccessfulRecoveryAsync(
                supervisor);

        Assert.Equal(
            expectedStartedAtUtc,
            statistics.LastRecoveryStartedAtUtc);

        Assert.Equal(
            expectedStartedAtUtc.AddSeconds(
                6),
            statistics.LastRecoveryCompletedAtUtc);

        Assert.Equal(
            TimeSpan.FromSeconds(
                6),
            statistics.LastRecoveryDuration);

        Assert.Equal(
            1,
            statistics.InitialConnectionAttemptCount);

        Assert.Equal(
            0,
            statistics.InitialConnectionFailureCount);

        Assert.Equal(
            2,
            statistics.ReconnectAttemptCount);

        Assert.Equal(
            1,
            statistics.ReconnectFailureCount);

        Assert.Equal(
            1,
            statistics.SuccessfulRecoveryCount);

        Assert.Same(
            replacementConnection,
            connectionManager.CurrentConnection);

        Assert.Equal(
            EndpointConnectionState.Ready,
            runtimeEndpoint.ConnectionStatus.State);

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await supervisionTask);
    }

    private static async Task<RuntimeEndpointConnectionStatistics>
        WaitForSuccessfulRecoveryAsync(
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

            if (statistics.SuccessfulRecoveryCount == 1)
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
        private DateTimeOffset _utcNow;
        private long _timestamp;

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
            return _timestamp;
        }

        public void Advance(
            TimeSpan duration)
        {
            if (duration < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(duration));
            }

            _utcNow =
                _utcNow.Add(
                    duration);

            _timestamp +=
                duration.Ticks;
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
        private readonly Queue<
            Func<
                CancellationToken,
                Task<ITransportConnection>>> _results =
            new();

        public void EnqueueConnection(
            ITransportConnection connection,
            Action? beforeCompletion = null)
        {
            ArgumentNullException.ThrowIfNull(
                connection);

            _results.Enqueue(
                cancellationToken =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    beforeCompletion?.Invoke();

                    return Task.FromResult(
                        connection);
                });
        }

        public void EnqueueFailure(
            Exception exception,
            Action? beforeCompletion = null)
        {
            ArgumentNullException.ThrowIfNull(
                exception);

            _results.Enqueue(
                cancellationToken =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    beforeCompletion?.Invoke();

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
        private readonly Action _beforeRecoveryCompletion;

        private readonly TaskCompletionSource<bool>
            _initialSynchronizationCompleted =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool>
            _recoverySynchronizationCompleted =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        private int _synchronizeCallCount;

        public TestRuntimeEndpointSynchronizer(
            Action beforeRecoveryCompletion)
        {
            _beforeRecoveryCompletion =
                beforeRecoveryCompletion
                ?? throw new ArgumentNullException(
                    nameof(beforeRecoveryCompletion));
        }

        public Task InitialSynchronizationCompleted =>
            _initialSynchronizationCompleted.Task;

        public Task RecoverySynchronizationCompleted =>
            _recoverySynchronizationCompleted.Task;

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

            _synchronizeCallCount++;

            if (_synchronizeCallCount == 1)
            {
                _initialSynchronizationCompleted.TrySetResult(
                    true);
            }
            else if (_synchronizeCallCount == 2)
            {
                _beforeRecoveryCompletion();

                _recoverySynchronizationCompleted.TrySetResult(
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