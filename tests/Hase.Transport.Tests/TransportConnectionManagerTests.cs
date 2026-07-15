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
    public void NewManager_ShouldHaveNoCurrentConnection()
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

        // Assert
        Assert.Null(
            currentConnection);

        Assert.Null(
            currentState);
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