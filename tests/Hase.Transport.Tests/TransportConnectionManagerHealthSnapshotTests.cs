namespace Hase.Transport.Tests;

public sealed class TransportConnectionManagerHealthSnapshotTests
{
    [Fact]
    public void GetHealthSnapshot_BeforeConnection_ShouldDescribeNoConnection()
    {
        // Arrange
        var factory =
            new TestTransportFactory();

        var manager =
            new TransportConnectionManager(
                factory);

        // Act
        TransportConnectionHealthSnapshot snapshot =
            manager.GetHealthSnapshot();

        // Assert
        Assert.False(
            snapshot.HasConnection);

        Assert.Null(
            snapshot.State);

        Assert.Null(
            snapshot.LastStateChangeUtc);

        Assert.Equal(
            0,
            snapshot.ReplacementCount);
    }

    [Fact]
    public async Task GetHealthSnapshot_AfterConnection_ShouldDescribeConnection()
    {
        // Arrange
        var connection =
            new TestTransportConnection(
                TransportConnectionState.Connected);

        var factory =
            new TestTransportFactory();

        factory.EnqueueConnection(
            connection);

        await using var manager =
            new TransportConnectionManager(
                factory);

        DateTimeOffset beforeConnectUtc =
            DateTimeOffset.UtcNow;

        await manager.ConnectAsync();

        DateTimeOffset afterConnectUtc =
            DateTimeOffset.UtcNow;

        // Act
        TransportConnectionHealthSnapshot snapshot =
            manager.GetHealthSnapshot();

        // Assert
        Assert.True(
            snapshot.HasConnection);

        Assert.Equal(
            TransportConnectionState.Connected,
            snapshot.State);

        Assert.NotNull(
            snapshot.LastStateChangeUtc);

        Assert.InRange(
            snapshot.LastStateChangeUtc.Value,
            beforeConnectUtc,
            afterConnectUtc);

        Assert.Equal(
            0,
            snapshot.ReplacementCount);
    }

    [Fact]
    public async Task GetHealthSnapshot_AfterFault_ShouldDescribeFaultedConnection()
    {
        // Arrange
        var connection =
            new TestTransportConnection(
                TransportConnectionState.Connected);

        var factory =
            new TestTransportFactory();

        factory.EnqueueConnection(
            connection);

        await using var manager =
            new TransportConnectionManager(
                factory);

        await manager.ConnectAsync();

        DateTimeOffset initialTimestamp =
            manager.LastStateChangeUtc
            ?? throw new InvalidOperationException(
                "The initial state-change timestamp was not established.");

        await Task.Delay(
            TimeSpan.FromMilliseconds(
                20));

        connection.TransitionTo(
            TransportConnectionState.Faulted);

        // Act
        TransportConnectionHealthSnapshot snapshot =
            manager.GetHealthSnapshot();

        // Assert
        Assert.True(
            snapshot.HasConnection);

        Assert.Equal(
            TransportConnectionState.Faulted,
            snapshot.State);

        Assert.NotNull(
            snapshot.LastStateChangeUtc);

        Assert.True(
            snapshot.LastStateChangeUtc.Value
            > initialTimestamp);

        Assert.Equal(
            0,
            snapshot.ReplacementCount);
    }

    [Fact]
    public async Task GetHealthSnapshot_AfterReplacement_ShouldDescribeReplacement()
    {
        // Arrange
        var previousConnection =
            new TestTransportConnection(
                TransportConnectionState.Connected);

        var replacementConnection =
            new TestTransportConnection(
                TransportConnectionState.Connected);

        var factory =
            new TestTransportFactory();

        factory.EnqueueConnection(
            previousConnection);

        factory.EnqueueConnection(
            replacementConnection);

        await using var manager =
            new TransportConnectionManager(
                factory);

        await manager.ConnectAsync();

        previousConnection.TransitionTo(
            TransportConnectionState.Faulted);

        DateTimeOffset faultTimestamp =
            manager.LastStateChangeUtc
            ?? throw new InvalidOperationException(
                "The fault timestamp was not established.");

        await Task.Delay(
            TimeSpan.FromMilliseconds(
                20));

        await manager.ReplaceFaultedAsync();

        // Act
        TransportConnectionHealthSnapshot snapshot =
            manager.GetHealthSnapshot();

        // Assert
        Assert.True(
            snapshot.HasConnection);

        Assert.Equal(
            TransportConnectionState.Connected,
            snapshot.State);

        Assert.NotNull(
            snapshot.LastStateChangeUtc);

        Assert.True(
            snapshot.LastStateChangeUtc.Value
            > faultTimestamp);

        Assert.Equal(
            1,
            snapshot.ReplacementCount);

        Assert.Equal(
            1,
            previousConnection.DisposeCallCount);

        Assert.Same(
            replacementConnection,
            manager.CurrentConnection);
    }

    [Fact]
    public async Task GetHealthSnapshot_PreviousSnapshot_ShouldRemainImmutable()
    {
        // Arrange
        var previousConnection =
            new TestTransportConnection(
                TransportConnectionState.Connected);

        var replacementConnection =
            new TestTransportConnection(
                TransportConnectionState.Connected);

        var factory =
            new TestTransportFactory();

        factory.EnqueueConnection(
            previousConnection);

        factory.EnqueueConnection(
            replacementConnection);

        await using var manager =
            new TransportConnectionManager(
                factory);

        await manager.ConnectAsync();

        TransportConnectionHealthSnapshot connectedSnapshot =
            manager.GetHealthSnapshot();

        previousConnection.TransitionTo(
            TransportConnectionState.Faulted);

        TransportConnectionHealthSnapshot faultedSnapshot =
            manager.GetHealthSnapshot();

        await manager.ReplaceFaultedAsync();

        TransportConnectionHealthSnapshot replacementSnapshot =
            manager.GetHealthSnapshot();

        // Act and Assert
        Assert.True(
            connectedSnapshot.HasConnection);

        Assert.Equal(
            TransportConnectionState.Connected,
            connectedSnapshot.State);

        Assert.Equal(
            0,
            connectedSnapshot.ReplacementCount);

        Assert.True(
            faultedSnapshot.HasConnection);

        Assert.Equal(
            TransportConnectionState.Faulted,
            faultedSnapshot.State);

        Assert.Equal(
            0,
            faultedSnapshot.ReplacementCount);

        Assert.True(
            replacementSnapshot.HasConnection);

        Assert.Equal(
            TransportConnectionState.Connected,
            replacementSnapshot.State);

        Assert.Equal(
            1,
            replacementSnapshot.ReplacementCount);
    }

    [Fact]
    public async Task GetHealthSnapshot_AfterDisposal_ShouldDescribeNoConnection()
    {
        // Arrange
        var connection =
            new TestTransportConnection(
                TransportConnectionState.Connected);

        var factory =
            new TestTransportFactory();

        factory.EnqueueConnection(
            connection);

        var manager =
            new TransportConnectionManager(
                factory);

        await manager.ConnectAsync();

        DateTimeOffset establishedTimestamp =
            manager.LastStateChangeUtc
            ?? throw new InvalidOperationException(
                "The initial state-change timestamp was not established.");

        await manager.DisposeAsync();

        // Act
        TransportConnectionHealthSnapshot snapshot =
            manager.GetHealthSnapshot();

        // Assert
        Assert.False(
            snapshot.HasConnection);

        Assert.Null(
            snapshot.State);

        Assert.Equal(
            establishedTimestamp,
            snapshot.LastStateChangeUtc);

        Assert.Equal(
            0,
            snapshot.ReplacementCount);

        Assert.Equal(
            1,
            connection.DisposeCallCount);
    }

    private sealed class TestTransportFactory
        : ITransportFactory
    {
        private readonly Queue<ITransportConnection>
            _connections =
            new();

        public void EnqueueConnection(
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
                    "No test transport connection was configured.");
            }

            return Task.FromResult(
                _connections.Dequeue());
        }
    }

    private sealed class TestTransportConnection
        : ITransportConnection,
          IAsyncDisposable
    {
        private TransportConnectionState _state;

        public TestTransportConnection(
            TransportConnectionState initialState)
        {
            _state =
                initialState;
        }

        public event EventHandler<
            TransportConnectionStateChangedEventArgs>?
            StateChanged;

        public TransportConnectionState State =>
            _state;

        public int DisposeCallCount
        {
            get;
            private set;
        }

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

        public ValueTask DisposeAsync()
        {
            DisposeCallCount++;

            if (_state
                != TransportConnectionState.Closed)
            {
                TransitionTo(
                    TransportConnectionState.Closed);
            }

            return ValueTask.CompletedTask;
        }

        public void TransitionTo(
            TransportConnectionState currentState)
        {
            TransportConnectionState previousState =
                _state;

            if (previousState == currentState)
            {
                return;
            }

            _state =
                currentState;

            StateChanged?.Invoke(
                this,
                new TransportConnectionStateChangedEventArgs(
                    previousState,
                    currentState));
        }
    }
}