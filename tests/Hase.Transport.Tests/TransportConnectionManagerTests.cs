namespace Hase.Transport.Tests;

public sealed class TransportConnectionManagerTests
{
    [Fact]
    public void Constructor_NullFactory_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new TransportConnectionManager(
                null!);
        }

        // Assert
        ArgumentNullException exception =
            Assert.Throws<
                ArgumentNullException>(
                    Act);

        Assert.Equal(
            "factory",
            exception.ParamName);
    }

    [Fact]
    public void NewManager_ShouldHaveNoCurrentConnectionOrDiagnostics()
    {
        // Arrange
        var factory =
            new TestTransportFactory();

        var manager =
            new TransportConnectionManager(
                factory);

        // Act
        ITransportConnection? currentConnection =
            manager.CurrentConnection;

        TransportConnectionState? currentState =
            manager.CurrentState;

        DateTimeOffset? lastStateChangeUtc =
            manager.LastStateChangeUtc;

        int replacementCount =
            manager.ReplacementCount;

        // Assert
        Assert.Null(
            currentConnection);

        Assert.Null(
            currentState);

        Assert.Null(
            lastStateChangeUtc);

        Assert.Equal(
            0,
            replacementCount);
    }

    [Fact]
    public async Task ConnectAsync_ShouldStoreAndReturnCreatedConnection()
    {
        // Arrange
        var expectedConnection =
            new TestTransportConnection(
                TransportConnectionState.Connected);

        var factory =
            new TestTransportFactory();

        factory.EnqueueConnection(
            expectedConnection);

        await using var manager =
            new TransportConnectionManager(
                factory);

        // Act
        ITransportConnection actualConnection =
            await manager.ConnectAsync();

        // Assert
        Assert.Same(
            expectedConnection,
            actualConnection);

        Assert.Same(
            expectedConnection,
            manager.CurrentConnection);

        Assert.Equal(
            TransportConnectionState.Connected,
            manager.CurrentState);

        Assert.Equal(
            1,
            factory.ConnectCallCount);
    }

    [Fact]
    public async Task ConnectAsync_ShouldEstablishStateChangeTimestamp()
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

        // Act
        await manager.ConnectAsync();

        DateTimeOffset afterConnectUtc =
            DateTimeOffset.UtcNow;

        // Assert
        Assert.NotNull(
            manager.LastStateChangeUtc);

        Assert.InRange(
            manager.LastStateChangeUtc.Value,
            beforeConnectUtc,
            afterConnectUtc);

        Assert.Equal(
            0,
            manager.ReplacementCount);
    }

    [Fact]
    public async Task ConnectAsync_SecondCall_ShouldThrow()
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

        // Act
        Task Act()
        {
            return manager.ConnectAsync();
        }

        // Assert
        InvalidOperationException exception =
            await Assert.ThrowsAsync<
                InvalidOperationException>(
                    Act);

        Assert.Equal(
            "The transport connection manager already owns "
            + "a connection.",
            exception.Message);

        Assert.Equal(
            1,
            factory.ConnectCallCount);

        Assert.Same(
            connection,
            manager.CurrentConnection);
    }

    [Fact]
    public async Task CurrentConnectionStateChange_ShouldAdvanceTimestamp()
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
                "The initial connection timestamp was not established.");

        await Task.Delay(
            TimeSpan.FromMilliseconds(
                20));

        // Act
        connection.TransitionTo(
            TransportConnectionState.Faulted);

        // Assert
        Assert.Equal(
            TransportConnectionState.Faulted,
            manager.CurrentState);

        Assert.NotNull(
            manager.LastStateChangeUtc);

        Assert.True(
            manager.LastStateChangeUtc.Value
            > initialTimestamp);
    }

    [Fact]
    public async Task ReplaceFaultedAsync_WithoutConnection_ShouldThrow()
    {
        // Arrange
        var factory =
            new TestTransportFactory();

        await using var manager =
            new TransportConnectionManager(
                factory);

        // Act
        Task Act()
        {
            return manager.ReplaceFaultedAsync();
        }

        // Assert
        InvalidOperationException exception =
            await Assert.ThrowsAsync<
                InvalidOperationException>(
                    Act);

        Assert.Equal(
            "The transport connection manager does not own "
            + "a connection.",
            exception.Message);

        Assert.Equal(
            0,
            factory.ConnectCallCount);

        Assert.Equal(
            0,
            manager.ReplacementCount);
    }

    [Fact]
    public async Task ReplaceFaultedAsync_ConnectedConnection_ShouldThrow()
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

        // Act
        Task Act()
        {
            return manager.ReplaceFaultedAsync();
        }

        // Assert
        InvalidOperationException exception =
            await Assert.ThrowsAsync<
                InvalidOperationException>(
                    Act);

        Assert.Equal(
            "Only a faulted transport connection can be replaced.",
            exception.Message);

        Assert.Equal(
            1,
            factory.ConnectCallCount);

        Assert.Same(
            connection,
            manager.CurrentConnection);

        Assert.Equal(
            0,
            connection.DisposeCallCount);

        Assert.Equal(
            0,
            manager.ReplacementCount);
    }

    [Fact]
    public async Task ReplaceFaultedAsync_ShouldReplaceAndDisposePreviousConnection()
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

        // Act
        ITransportConnection actualConnection =
            await manager.ReplaceFaultedAsync();

        // Assert
        Assert.Same(
            replacementConnection,
            actualConnection);

        Assert.Same(
            replacementConnection,
            manager.CurrentConnection);

        Assert.Equal(
            TransportConnectionState.Connected,
            manager.CurrentState);

        Assert.Equal(
            2,
            factory.ConnectCallCount);

        Assert.Equal(
            1,
            previousConnection.DisposeCallCount);

        Assert.Equal(
            TransportConnectionState.Closed,
            previousConnection.State);

        Assert.Equal(
            0,
            replacementConnection.DisposeCallCount);

        Assert.Equal(
            1,
            manager.ReplacementCount);
    }

    [Fact]
    public async Task ReplaceFaultedAsync_ShouldAdvanceTimestamp()
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
                "The fault timestamp was not recorded.");

        await Task.Delay(
            TimeSpan.FromMilliseconds(
                20));

        // Act
        await manager.ReplaceFaultedAsync();

        // Assert
        Assert.NotNull(
            manager.LastStateChangeUtc);

        Assert.True(
            manager.LastStateChangeUtc.Value
            > faultTimestamp);

        Assert.Equal(
            1,
            manager.ReplacementCount);
    }

    [Fact]
    public async Task ReplaceFaultedAsync_FactoryThrows_ShouldPreservePreviousConnection()
    {
        // Arrange
        var previousConnection =
            new TestTransportConnection(
                TransportConnectionState.Connected);

        var expectedException =
            new InvalidOperationException(
                "Connection creation failed.");

        var factory =
            new TestTransportFactory();

        factory.EnqueueConnection(
            previousConnection);

        factory.EnqueueException(
            expectedException);

        await using var manager =
            new TransportConnectionManager(
                factory);

        await manager.ConnectAsync();

        previousConnection.TransitionTo(
            TransportConnectionState.Faulted);

        DateTimeOffset? faultTimestamp =
            manager.LastStateChangeUtc;

        // Act
        Task Act()
        {
            return manager.ReplaceFaultedAsync();
        }

        // Assert
        InvalidOperationException actualException =
            await Assert.ThrowsAsync<
                InvalidOperationException>(
                    Act);

        Assert.Same(
            expectedException,
            actualException);

        Assert.Same(
            previousConnection,
            manager.CurrentConnection);

        Assert.Equal(
            TransportConnectionState.Faulted,
            manager.CurrentState);

        Assert.Equal(
            0,
            previousConnection.DisposeCallCount);

        Assert.Equal(
            2,
            factory.ConnectCallCount);

        Assert.Equal(
            0,
            manager.ReplacementCount);

        Assert.Equal(
            faultTimestamp,
            manager.LastStateChangeUtc);
    }

    [Fact]
    public async Task DetachedPreviousConnectionStateChange_ShouldBeIgnored()
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

        await manager.ReplaceFaultedAsync();

        DateTimeOffset replacementTimestamp =
            manager.LastStateChangeUtc
            ?? throw new InvalidOperationException(
                "The replacement timestamp was not recorded.");

        await Task.Delay(
            TimeSpan.FromMilliseconds(
                20));

        // Act
        previousConnection.TransitionTo(
            TransportConnectionState.Connected);

        // Assert
        Assert.Same(
            replacementConnection,
            manager.CurrentConnection);

        Assert.Equal(
            TransportConnectionState.Connected,
            manager.CurrentState);

        Assert.Equal(
            replacementTimestamp,
            manager.LastStateChangeUtc);

        Assert.Equal(
            1,
            manager.ReplacementCount);
    }

    [Fact]
    public async Task ReplacementConnectionStateChange_ShouldAdvanceTimestamp()
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

        await manager.ReplaceFaultedAsync();

        DateTimeOffset replacementTimestamp =
            manager.LastStateChangeUtc
            ?? throw new InvalidOperationException(
                "The replacement timestamp was not recorded.");

        await Task.Delay(
            TimeSpan.FromMilliseconds(
                20));

        // Act
        replacementConnection.TransitionTo(
            TransportConnectionState.Faulted);

        // Assert
        Assert.Equal(
            TransportConnectionState.Faulted,
            manager.CurrentState);

        Assert.NotNull(
            manager.LastStateChangeUtc);

        Assert.True(
            manager.LastStateChangeUtc.Value
            > replacementTimestamp);

        Assert.Equal(
            1,
            manager.ReplacementCount);
    }

    [Fact]
    public async Task MultipleSuccessfulReplacements_ShouldIncrementCount()
    {
        // Arrange
        var firstConnection =
            new TestTransportConnection(
                TransportConnectionState.Connected);

        var secondConnection =
            new TestTransportConnection(
                TransportConnectionState.Connected);

        var thirdConnection =
            new TestTransportConnection(
                TransportConnectionState.Connected);

        var factory =
            new TestTransportFactory();

        factory.EnqueueConnection(
            firstConnection);

        factory.EnqueueConnection(
            secondConnection);

        factory.EnqueueConnection(
            thirdConnection);

        await using var manager =
            new TransportConnectionManager(
                factory);

        await manager.ConnectAsync();

        firstConnection.TransitionTo(
            TransportConnectionState.Faulted);

        await manager.ReplaceFaultedAsync();

        secondConnection.TransitionTo(
            TransportConnectionState.Faulted);

        // Act
        await manager.ReplaceFaultedAsync();

        // Assert
        Assert.Same(
            thirdConnection,
            manager.CurrentConnection);

        Assert.Equal(
            TransportConnectionState.Connected,
            manager.CurrentState);

        Assert.Equal(
            2,
            manager.ReplacementCount);

        Assert.Equal(
            1,
            firstConnection.DisposeCallCount);

        Assert.Equal(
            1,
            secondConnection.DisposeCallCount);

        Assert.Equal(
            0,
            thirdConnection.DisposeCallCount);
    }

    [Fact]
    public async Task DisposeAsync_ShouldDisposeAndClearCurrentConnection()
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

        // Act
        await manager.DisposeAsync();

        // Assert
        Assert.Equal(
            1,
            connection.DisposeCallCount);

        Assert.Equal(
            TransportConnectionState.Closed,
            connection.State);

        Assert.Null(
            manager.CurrentConnection);

        Assert.Null(
            manager.CurrentState);

        Assert.Equal(
            0,
            manager.ReplacementCount);
    }

    [Fact]
    public async Task DisposeAsync_ShouldNotRecordDetachedConnectionCloseTransition()
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

        DateTimeOffset timestampBeforeDisposal =
            manager.LastStateChangeUtc
            ?? throw new InvalidOperationException(
                "The initial timestamp was not established.");

        await Task.Delay(
            TimeSpan.FromMilliseconds(
                20));

        // Act
        await manager.DisposeAsync();

        // Assert
        Assert.Equal(
            timestampBeforeDisposal,
            manager.LastStateChangeUtc);

        Assert.Null(
            manager.CurrentConnection);

        Assert.Null(
            manager.CurrentState);
    }

    [Fact]
    public async Task DisposeAsync_RepeatedCall_ShouldDisposeConnectionOnce()
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

        // Act
        await manager.DisposeAsync();
        await manager.DisposeAsync();

        // Assert
        Assert.Equal(
            1,
            connection.DisposeCallCount);

        Assert.Null(
            manager.CurrentConnection);

        Assert.Null(
            manager.CurrentState);
    }

    [Fact]
    public async Task ConnectAsync_AfterManagerDisposal_ShouldThrow()
    {
        // Arrange
        var factory =
            new TestTransportFactory();

        var manager =
            new TransportConnectionManager(
                factory);

        await manager.DisposeAsync();

        // Act
        Task Act()
        {
            return manager.ConnectAsync();
        }

        // Assert
        await Assert.ThrowsAsync<
            ObjectDisposedException>(
                Act);

        Assert.Equal(
            0,
            factory.ConnectCallCount);
    }

    [Fact]
    public async Task ReplaceFaultedAsync_AfterManagerDisposal_ShouldThrow()
    {
        // Arrange
        var factory =
            new TestTransportFactory();

        var manager =
            new TransportConnectionManager(
                factory);

        await manager.DisposeAsync();

        // Act
        Task Act()
        {
            return manager.ReplaceFaultedAsync();
        }

        // Assert
        await Assert.ThrowsAsync<
            ObjectDisposedException>(
                Act);

        Assert.Equal(
            0,
            factory.ConnectCallCount);
    }

    private sealed class TestTransportFactory
        : ITransportFactory
    {
        private readonly Queue<
            Func<ITransportConnection>> _connectionResults =
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

            _connectionResults.Enqueue(
                () => connection);
        }

        public void EnqueueException(
            Exception exception)
        {
            ArgumentNullException.ThrowIfNull(
                exception);

            _connectionResults.Enqueue(
                () => throw exception);
        }

        public Task<ITransportConnection> ConnectAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ConnectCallCount++;

            if (_connectionResults.Count == 0)
            {
                throw new InvalidOperationException(
                    "No test connection result was configured.");
            }

            ITransportConnection connection =
                _connectionResults
                    .Dequeue()
                    .Invoke();

            return Task.FromResult(
                connection);
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

            if (_state
                != TransportConnectionState.Connected)
            {
                throw new InvalidOperationException(
                    "The test transport connection is not connected.");
            }

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