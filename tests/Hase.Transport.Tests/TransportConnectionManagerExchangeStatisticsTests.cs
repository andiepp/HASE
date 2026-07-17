namespace Hase.Transport.Tests;

public sealed class TransportConnectionManagerExchangeStatisticsTests
{
    [Fact]
    public void NewManager_ShouldHaveEmptyExchangeStatistics()
    {
        // Arrange
        var manager =
            new TransportConnectionManager(
                new TestTransportFactory());

        // Act
        TransportExchangeStatistics statistics =
            manager.GetExchangeStatistics();

        // Assert
        Assert.Equal(
            TransportExchangeStatistics.Empty,
            statistics);
    }

    [Fact]
    public async Task ConnectAsync_TraceCapableConnection_ShouldCollectExchanges()
    {
        // Arrange
        var connection =
            new TestTraceConnection(
                TransportConnectionState.Connected);

        var factory =
            new TestTransportFactory();

        factory.Enqueue(
            connection);

        await using var manager =
            new TransportConnectionManager(
                factory);

        await manager.ConnectAsync();

        TransportExchangeTrace trace =
            CreateTrace(
                sequenceNumber:
                    1,
                completedAtUtc:
                    DateTimeOffset.UnixEpoch.AddSeconds(
                        1),
                outcome:
                    TransportExchangeOutcome.Succeeded,
                requestByteCount:
                    32,
                responseByteCount:
                    967);

        // Act
        connection.Publish(
            trace);

        TransportExchangeStatistics statistics =
            manager.GetExchangeStatistics();

        // Assert
        Assert.Equal(
            1,
            connection.ObserverCount);

        Assert.Equal(
            1,
            statistics.CompletedExchangeCount);

        Assert.Equal(
            1,
            statistics.SuccessfulExchangeCount);

        Assert.Equal(
            32,
            statistics.TotalRequestByteCount);

        Assert.Equal(
            967,
            statistics.TotalResponseByteCount);

        Assert.Equal(
            trace.CompletedAtUtc,
            statistics.LastCompletedAtUtc);

        Assert.Equal(
            TransportExchangeOutcome.Succeeded,
            statistics.LastOutcome);
    }

    [Fact]
    public async Task ConnectAsync_ConnectionWithoutTracing_ShouldRemainSupported()
    {
        // Arrange
        var connection =
            new TestConnectionWithoutTracing();

        var factory =
            new TestTransportFactory();

        factory.Enqueue(
            connection);

        await using var manager =
            new TransportConnectionManager(
                factory);

        // Act
        ITransportConnection actualConnection =
            await manager.ConnectAsync();

        TransportExchangeStatistics statistics =
            manager.GetExchangeStatistics();

        // Assert
        Assert.Same(
            connection,
            actualConnection);

        Assert.Equal(
            TransportExchangeStatistics.Empty,
            statistics);
    }

    [Fact]
    public async Task ReplaceFaultedAsync_ShouldPreserveAggregateStatistics()
    {
        // Arrange
        var initialConnection =
            new TestTraceConnection(
                TransportConnectionState.Connected);

        var replacementConnection =
            new TestTraceConnection(
                TransportConnectionState.Connected);

        var factory =
            new TestTransportFactory();

        factory.Enqueue(
            initialConnection);

        factory.Enqueue(
            replacementConnection);

        await using var manager =
            new TransportConnectionManager(
                factory);

        await manager.ConnectAsync();

        initialConnection.Publish(
            CreateTrace(
                sequenceNumber:
                    1,
                completedAtUtc:
                    DateTimeOffset.UnixEpoch.AddSeconds(
                        1),
                outcome:
                    TransportExchangeOutcome.Succeeded,
                requestByteCount:
                    20,
                responseByteCount:
                    100));

        initialConnection.TransitionTo(
            TransportConnectionState.Faulted);

        // Act
        await manager.ReplaceFaultedAsync();

        replacementConnection.Publish(
            CreateTrace(
                sequenceNumber:
                    1,
                completedAtUtc:
                    DateTimeOffset.UnixEpoch.AddSeconds(
                        2),
                outcome:
                    TransportExchangeOutcome.Failed,
                requestByteCount:
                    30,
                responseByteCount:
                    0));

        TransportExchangeStatistics statistics =
            manager.GetExchangeStatistics();

        // Assert
        Assert.Equal(
            0,
            initialConnection.ObserverCount);

        Assert.Equal(
            1,
            replacementConnection.ObserverCount);

        Assert.True(
            initialConnection.IsDisposed);

        Assert.Same(
            replacementConnection,
            manager.CurrentConnection);

        Assert.Equal(
            2,
            statistics.CompletedExchangeCount);

        Assert.Equal(
            1,
            statistics.SuccessfulExchangeCount);

        Assert.Equal(
            1,
            statistics.FailedExchangeCount);

        Assert.Equal(
            0,
            statistics.CancelledExchangeCount);

        Assert.Equal(
            50,
            statistics.TotalRequestByteCount);

        Assert.Equal(
            100,
            statistics.TotalResponseByteCount);

        Assert.Equal(
            TimeSpan.FromMilliseconds(
                20),
            statistics.TotalDuration);

        Assert.Equal(
            DateTimeOffset.UnixEpoch.AddSeconds(
                2),
            statistics.LastCompletedAtUtc);

        Assert.Equal(
            TransportExchangeOutcome.Failed,
            statistics.LastOutcome);
    }

    [Fact]
    public async Task ReplaceFaultedAsync_DetachedConnectionTrace_ShouldNotBeCollected()
    {
        // Arrange
        var initialConnection =
            new TestTraceConnection(
                TransportConnectionState.Faulted);

        var replacementConnection =
            new TestTraceConnection(
                TransportConnectionState.Connected);

        var factory =
            new TestTransportFactory();

        factory.Enqueue(
            initialConnection);

        factory.Enqueue(
            replacementConnection);

        await using var manager =
            new TransportConnectionManager(
                factory);

        await manager.ConnectAsync();

        await manager.ReplaceFaultedAsync();

        // Act
        initialConnection.Publish(
            CreateTrace(
                sequenceNumber:
                    1,
                completedAtUtc:
                    DateTimeOffset.UnixEpoch.AddSeconds(
                        1),
                outcome:
                    TransportExchangeOutcome.Failed,
                requestByteCount:
                    30,
                responseByteCount:
                    0));

        // Assert
        Assert.Equal(
            TransportExchangeStatistics.Empty,
            manager.GetExchangeStatistics());
    }

    [Fact]
    public async Task DisposeAsync_ShouldDetachStatisticsCollector()
    {
        // Arrange
        var connection =
            new TestTraceConnection(
                TransportConnectionState.Connected);

        var factory =
            new TestTransportFactory();

        factory.Enqueue(
            connection);

        var manager =
            new TransportConnectionManager(
                factory);

        await manager.ConnectAsync();

        // Act
        await manager.DisposeAsync();

        connection.Publish(
            CreateTrace(
                sequenceNumber:
                    1,
                completedAtUtc:
                    DateTimeOffset.UnixEpoch.AddSeconds(
                        1),
                outcome:
                    TransportExchangeOutcome.Succeeded,
                requestByteCount:
                    20,
                responseByteCount:
                    100));

        // Assert
        Assert.Equal(
            0,
            connection.ObserverCount);

        Assert.True(
            connection.IsDisposed);

        Assert.Equal(
            TransportExchangeStatistics.Empty,
            manager.GetExchangeStatistics());
    }

    private static TransportExchangeTrace CreateTrace(
        long sequenceNumber,
        DateTimeOffset completedAtUtc,
        TransportExchangeOutcome outcome,
        int requestByteCount,
        int responseByteCount)
    {
        TimeSpan duration =
            TimeSpan.FromMilliseconds(
                10);

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
                outcome,
            connectionState:
                outcome == TransportExchangeOutcome.Succeeded
                    ? TransportConnectionState.Connected
                    : TransportConnectionState.Faulted,
            exceptionType:
                outcome == TransportExchangeOutcome.Succeeded
                    ? null
                    : typeof(IOException).FullName,
            exceptionMessage:
                outcome == TransportExchangeOutcome.Succeeded
                    ? null
                    : "The transport exchange failed.");
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

    private sealed class TestTraceConnection
        : ITransportConnection,
          ITransportExchangeTraceSource,
          IAsyncDisposable
    {
        private readonly object _syncRoot =
            new();

        private readonly List<ITransportExchangeTraceObserver> _observers =
            new();

        public TestTraceConnection(
            TransportConnectionState state)
        {
            State =
                state;
        }

        public event EventHandler<
            TransportConnectionStateChangedEventArgs>?
            StateChanged;

        public TransportConnectionState State
        {
            get;
            private set;
        }

        public int ObserverCount
        {
            get
            {
                lock (_syncRoot)
                {
                    return _observers.Count;
                }
            }
        }

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

    private sealed class TestConnectionWithoutTracing
        : ITransportConnection
    {
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
    }
}
