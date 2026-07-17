using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Runtime;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class RuntimeEndpointConnectionSupervisorConnectedDiagnosticsTests
{
    [Fact]
    public async Task GetDiagnostics_AfterConnectionAndExchange_ShouldCombineSnapshots()
    {
        // Arrange
        var connection =
            new TestTraceConnection();

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

        var supervisor =
            new RuntimeEndpointConnectionSupervisor(
                coordinator,
                new ImmediateReconnectPolicy());

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task supervisionTask =
            supervisor.RunAsync(
                cancellationTokenSource.Token);

        await synchronizer.SynchronizationCompleted;

        DateTimeOffset completedAtUtc =
            DateTimeOffset.UnixEpoch.AddMinutes(
                1);

        connection.Publish(
            new TransportExchangeTrace(
                sequenceNumber:
                    1,
                startedAtUtc:
                    completedAtUtc.Subtract(
                        TimeSpan.FromMilliseconds(
                            25)),
                completedAtUtc:
                    completedAtUtc,
                duration:
                    TimeSpan.FromMilliseconds(
                        25),
                requestByteCount:
                    32,
                responseByteCount:
                    967,
                outcome:
                    TransportExchangeOutcome.Succeeded,
                connectionState:
                    TransportConnectionState.Connected));

        // Act
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
            0,
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
            0,
            diagnostics.ConnectionStatistics
                .ReconnectAttemptCount);

        Assert.Equal(
            0,
            diagnostics.ConnectionStatistics
                .ReconnectFailureCount);

        Assert.Equal(
            0,
            diagnostics.ConnectionStatistics
                .SuccessfulRecoveryCount);

        Assert.Equal(
            1,
            diagnostics.ExchangeStatistics
                .CompletedExchangeCount);

        Assert.Equal(
            1,
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
            32,
            diagnostics.ExchangeStatistics
                .TotalRequestByteCount);

        Assert.Equal(
            967,
            diagnostics.ExchangeStatistics
                .TotalResponseByteCount);

        Assert.Equal(
            TimeSpan.FromMilliseconds(
                25),
            diagnostics.ExchangeStatistics
                .TotalDuration);

        Assert.Equal(
            completedAtUtc,
            diagnostics.ExchangeStatistics
                .LastCompletedAtUtc);

        Assert.Equal(
            TransportExchangeOutcome.Succeeded,
            diagnostics.ExchangeStatistics
                .LastOutcome);

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
        private readonly ITransportConnection _connection;

        public TestTransportFactory(
            ITransportConnection connection)
        {
            _connection =
                connection
                ?? throw new ArgumentNullException(
                    nameof(connection));
        }

        public Task<ITransportConnection> ConnectAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

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

    private sealed class TestTraceConnection
        : ITransportConnection,
          ITransportExchangeTraceSource
    {
        private readonly object _syncRoot =
            new();

        private readonly List<ITransportExchangeTraceObserver> _observers =
            new();

        public event EventHandler<
            TransportConnectionStateChangedEventArgs>?
            StateChanged
        {
            add
            {
            }

            remove
            {
            }
        }

        public TransportConnectionState State =>
            TransportConnectionState.Connected;

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
    }
}