using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Runtime;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class RuntimeEndpointConnectionSupervisorRecoveryDiagnosticsTests
{
    [Fact]
    public async Task GetDiagnostics_AfterRecovery_ShouldCombineBothConnections()
    {
        // Arrange
        var initialConnection =
            new TestTraceConnection();

        var replacementConnection =
            new TestTraceConnection();

        var factory =
            new TestTransportFactory();

        factory.Enqueue(
            initialConnection);

        factory.Enqueue(
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

        var supervisor =
            new RuntimeEndpointConnectionSupervisor(
                coordinator,
                new ImmediateReconnectPolicy());

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task supervisionTask =
            supervisor.RunAsync(
                cancellationTokenSource.Token);

        await synchronizer.WaitForSynchronizationAsync(
            expectedCount:
                1);

        initialConnection.Publish(
            CreateSuccessfulTrace(
                sequenceNumber:
                    1,
                completedAtUtc:
                    DateTimeOffset.UnixEpoch.AddMinutes(
                        1),
                requestByteCount:
                    20,
                responseByteCount:
                    100,
                duration:
                    TimeSpan.FromMilliseconds(
                        10)));

        // Act
        initialConnection.TransitionTo(
            TransportConnectionState.Faulted);

        await synchronizer.WaitForSynchronizationAsync(
            expectedCount:
                2);

        await WaitForSuccessfulRecoveryAsync(
            supervisor);

        replacementConnection.Publish(
            CreateSuccessfulTrace(
                sequenceNumber:
                    1,
                completedAtUtc:
                    DateTimeOffset.UnixEpoch.AddMinutes(
                        2),
                requestByteCount:
                    30,
                responseByteCount:
                    200,
                duration:
                    TimeSpan.FromMilliseconds(
                        20)));

        RuntimeEndpointConnectionDiagnostics diagnostics =
            supervisor.GetDiagnostics();

        // Assert
        Assert.True(
            diagnostics.TransportHealth.HasConnection);

        Assert.Equal(
            TransportConnectionState.Connected,
            diagnostics.TransportHealth.State);

        Assert.NotNull(
            diagnostics.TransportHealth.LastStateChangeUtc);

        Assert.Equal(
            1,
            diagnostics.TransportHealth.ReplacementCount);

        Assert.Equal(
            1,
            diagnostics.ConnectionStatistics
                .InitialConnectionAttemptCount);

        Assert.Equal(
            0,
            diagnostics.ConnectionStatistics
                .InitialConnectionFailureCount);

        Assert.Equal(
            1,
            diagnostics.ConnectionStatistics
                .ReconnectAttemptCount);

        Assert.Equal(
            0,
            diagnostics.ConnectionStatistics
                .ReconnectFailureCount);

        Assert.Equal(
            1,
            diagnostics.ConnectionStatistics
                .SuccessfulRecoveryCount);

        Assert.NotNull(
            diagnostics.ConnectionStatistics
                .LastRecoveryStartedAtUtc);

        Assert.NotNull(
            diagnostics.ConnectionStatistics
                .LastRecoveryCompletedAtUtc);

        Assert.NotNull(
            diagnostics.ConnectionStatistics
                .LastRecoveryDuration);

        Assert.True(
            diagnostics.ConnectionStatistics
                    .LastRecoveryDuration
                >= TimeSpan.Zero);

        Assert.Equal(
            2,
            diagnostics.ExchangeStatistics
                .CompletedExchangeCount);

        Assert.Equal(
            2,
            diagnostics.ExchangeStatistics
                .SuccessfulExchangeCount);

        Assert.Equal(
            0,
            diagnostics.ExchangeStatistics
                .FailedExchangeCount);

        Assert.Equal(
            0,
            diagnostics.ExchangeStatistics
                .CancelledExchangeCount);

        Assert.Equal(
            50,
            diagnostics.ExchangeStatistics
                .TotalRequestByteCount);

        Assert.Equal(
            300,
            diagnostics.ExchangeStatistics
                .TotalResponseByteCount);

        Assert.Equal(
            TimeSpan.FromMilliseconds(
                30),
            diagnostics.ExchangeStatistics
                .TotalDuration);

        Assert.Equal(
            DateTimeOffset.UnixEpoch.AddMinutes(
                2),
            diagnostics.ExchangeStatistics
                .LastCompletedAtUtc);

        Assert.Equal(
            TransportExchangeOutcome.Succeeded,
            diagnostics.ExchangeStatistics
                .LastOutcome);

        Assert.Same(
            replacementConnection,
            connectionManager.CurrentConnection);

        Assert.True(
            initialConnection.IsDisposed);

        Assert.False(
            replacementConnection.IsDisposed);

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(
                async () =>
                    await supervisionTask);
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

    private static TransportExchangeTrace CreateSuccessfulTrace(
        long sequenceNumber,
        DateTimeOffset completedAtUtc,
        int requestByteCount,
        int responseByteCount,
        TimeSpan duration)
    {
        return new TransportExchangeTrace(
            sequenceNumber:
                sequenceNumber,
            startedAtUtc:
                completedAtUtc.Subtract(
                    duration),
            completedAtUtc:
                completedAtUtc,
            duration:
                duration,
            requestByteCount:
                requestByteCount,
            responseByteCount:
                responseByteCount,
            outcome:
                TransportExchangeOutcome.Succeeded,
            connectionState:
                TransportConnectionState.Connected);
    }

    private static async Task WaitForSuccessfulRecoveryAsync(
        RuntimeEndpointConnectionSupervisor supervisor)
    {
        using var timeoutCancellationTokenSource =
            new CancellationTokenSource(
                TimeSpan.FromSeconds(
                    5));

        while (supervisor
                .GetStatistics()
                .SuccessfulRecoveryCount
            < 1)
        {
            await Task.Delay(
                TimeSpan.FromMilliseconds(
                    10),
                timeoutCancellationTokenSource.Token);
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
        private readonly Queue<ITransportConnection> _connections =
            new();

        public void Enqueue(
            ITransportConnection connection)
        {
            ArgumentNullException.ThrowIfNull(
                connection);

            _connections.Enqueue(
                connection);
        }

        public Task<ITransportConnection> ConnectAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

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
        private readonly object _syncRoot =
            new();

        private readonly List<TaskCompletionSource<bool>> _waiters =
            new();

        private int _synchronizationCount;

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

            TaskCompletionSource<bool>[] completedWaiters;

            lock (_syncRoot)
            {
                _synchronizationCount++;

                completedWaiters =
                    _waiters
                        .Where(
                            waiter =>
                                (int)waiter.Task.AsyncState!
                                <= _synchronizationCount)
                        .ToArray();

                foreach (TaskCompletionSource<bool> waiter
                    in completedWaiters)
                {
                    _waiters.Remove(
                        waiter);
                }
            }

            foreach (TaskCompletionSource<bool> waiter
                in completedWaiters)
            {
                waiter.TrySetResult(
                    true);
            }

            return Task.CompletedTask;
        }

        public Task WaitForSynchronizationAsync(
            int expectedCount)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
                expectedCount);

            lock (_syncRoot)
            {
                if (_synchronizationCount >= expectedCount)
                {
                    return Task.CompletedTask;
                }

                var waiter =
                    new TaskCompletionSource<bool>(
                        state:
                            expectedCount,
                        creationOptions:
                            TaskCreationOptions.RunContinuationsAsynchronously);

                _waiters.Add(
                    waiter);

                return waiter.Task.WaitAsync(
                    TimeSpan.FromSeconds(
                        5));
            }
        }
    }

    private sealed class TestTraceConnection
        : ITransportConnection,
          ITransportExchangeTraceSource,
          IAsyncDisposable
    {
        private readonly object _syncRoot =
            new();

        private readonly List<ITransportExchangeTraceObserver> _observers =
            new();

        public event EventHandler<
            TransportConnectionStateChangedEventArgs>?
            StateChanged;

        public TransportConnectionState State
        {
            get;
            private set;
        } =
            TransportConnectionState.Connected;

        public bool IsDisposed
        {
            get;
            private set;
        }

        public Task<byte[]> ExchangeAsync(
            byte[] request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public void SubscribeTrace(
            ITransportExchangeTraceObserver observer)
        {
            ArgumentNullException.ThrowIfNull(
                observer);

            lock (_syncRoot)
            {
                if (!_observers.Contains(
                        observer))
                {
                    _observers.Add(
                        observer);
                }
            }
        }

        public void UnsubscribeTrace(
            ITransportExchangeTraceObserver observer)
        {
            ArgumentNullException.ThrowIfNull(
                observer);

            lock (_syncRoot)
            {
                _observers.Remove(
                    observer);
            }
        }

        public void Publish(
            TransportExchangeTrace trace)
        {
            ArgumentNullException.ThrowIfNull(
                trace);

            ITransportExchangeTraceObserver[] observers;

            lock (_syncRoot)
            {
                observers =
                    _observers.ToArray();
            }

            foreach (ITransportExchangeTraceObserver observer
                in observers)
            {
                observer.OnTransportExchangeCompleted(
                    trace);
            }
        }

        public void TransitionTo(
            TransportConnectionState newState)
        {
            TransportConnectionState previousState =
                State;

            State =
                newState;

            StateChanged?.Invoke(
                this,
                new TransportConnectionStateChangedEventArgs(
                    previousState,
                    newState));
        }

        public ValueTask DisposeAsync()
        {
            IsDisposed =
                true;

            return ValueTask.CompletedTask;
        }
    }
}
