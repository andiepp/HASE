namespace Hase.Transport.Tests;

public sealed class TransportConnectionManagerHealthChangedTests
{
    [Fact]
    public async Task ConnectAsync_ShouldRaiseOneHealthChangedNotification()
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

        var notifications =
            new List<HealthChangedNotification>();

        manager.HealthChanged +=
            (
                sender,
                eventArgs) =>
            {
                notifications.Add(
                    new HealthChangedNotification(
                        sender,
                        eventArgs));
            };

        // Act
        await manager.ConnectAsync();

        // Assert
        HealthChangedNotification notification =
            Assert.Single(
                notifications);

        Assert.Same(
            manager,
            notification.Sender);

        Assert.False(
            notification.EventArgs
                .PreviousHealth
                .HasConnection);

        Assert.Null(
            notification.EventArgs
                .PreviousHealth
                .State);

        Assert.Null(
            notification.EventArgs
                .PreviousHealth
                .LastStateChangeUtc);

        Assert.Equal(
            0,
            notification.EventArgs
                .PreviousHealth
                .ReplacementCount);

        Assert.True(
            notification.EventArgs
                .CurrentHealth
                .HasConnection);

        Assert.Equal(
            TransportConnectionState.Connected,
            notification.EventArgs
                .CurrentHealth
                .State);

        Assert.NotNull(
            notification.EventArgs
                .CurrentHealth
                .LastStateChangeUtc);

        Assert.Equal(
            0,
            notification.EventArgs
                .CurrentHealth
                .ReplacementCount);
    }

    [Fact]
    public async Task CurrentConnectionStateChange_ShouldRaiseOneNotification()
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

        var notifications =
            new List<HealthChangedNotification>();

        manager.HealthChanged +=
            (
                sender,
                eventArgs) =>
            {
                notifications.Add(
                    new HealthChangedNotification(
                        sender,
                        eventArgs));
            };

        DateTimeOffset previousTimestamp =
            manager.LastStateChangeUtc
            ?? throw new InvalidOperationException(
                "The connection timestamp was not established.");

        await Task.Delay(
            TimeSpan.FromMilliseconds(
                20));

        // Act
        connection.TransitionTo(
            TransportConnectionState.Faulted);

        // Assert
        HealthChangedNotification notification =
            Assert.Single(
                notifications);

        Assert.Same(
            manager,
            notification.Sender);

        Assert.True(
            notification.EventArgs
                .PreviousHealth
                .HasConnection);

        Assert.Equal(
            TransportConnectionState.Connected,
            notification.EventArgs
                .PreviousHealth
                .State);

        Assert.Equal(
            previousTimestamp,
            notification.EventArgs
                .PreviousHealth
                .LastStateChangeUtc);

        Assert.Equal(
            0,
            notification.EventArgs
                .PreviousHealth
                .ReplacementCount);

        Assert.True(
            notification.EventArgs
                .CurrentHealth
                .HasConnection);

        Assert.Equal(
            TransportConnectionState.Faulted,
            notification.EventArgs
                .CurrentHealth
                .State);

        Assert.NotNull(
            notification.EventArgs
                .CurrentHealth
                .LastStateChangeUtc);

        Assert.True(
            notification.EventArgs
                .CurrentHealth
                .LastStateChangeUtc
            > previousTimestamp);

        Assert.Equal(
            0,
            notification.EventArgs
                .CurrentHealth
                .ReplacementCount);
    }

    [Fact]
    public async Task ReplaceFaultedAsync_ShouldRaiseOneReplacementNotification()
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

        var notifications =
            new List<HealthChangedNotification>();

        manager.HealthChanged +=
            (
                sender,
                eventArgs) =>
            {
                notifications.Add(
                    new HealthChangedNotification(
                        sender,
                        eventArgs));
            };

        await Task.Delay(
            TimeSpan.FromMilliseconds(
                20));

        // Act
        ITransportConnection actualReplacement =
            await manager.ReplaceFaultedAsync();

        // Assert
        Assert.Same(
            replacementConnection,
            actualReplacement);

        HealthChangedNotification notification =
            Assert.Single(
                notifications);

        Assert.Same(
            manager,
            notification.Sender);

        Assert.True(
            notification.EventArgs
                .PreviousHealth
                .HasConnection);

        Assert.Equal(
            TransportConnectionState.Faulted,
            notification.EventArgs
                .PreviousHealth
                .State);

        Assert.Equal(
            faultTimestamp,
            notification.EventArgs
                .PreviousHealth
                .LastStateChangeUtc);

        Assert.Equal(
            0,
            notification.EventArgs
                .PreviousHealth
                .ReplacementCount);

        Assert.True(
            notification.EventArgs
                .CurrentHealth
                .HasConnection);

        Assert.Equal(
            TransportConnectionState.Connected,
            notification.EventArgs
                .CurrentHealth
                .State);

        Assert.NotNull(
            notification.EventArgs
                .CurrentHealth
                .LastStateChangeUtc);

        Assert.True(
            notification.EventArgs
                .CurrentHealth
                .LastStateChangeUtc
            > faultTimestamp);

        Assert.Equal(
            1,
            notification.EventArgs
                .CurrentHealth
                .ReplacementCount);

        Assert.Equal(
            1,
            previousConnection.DisposeCallCount);

        Assert.Same(
            replacementConnection,
            manager.CurrentConnection);
    }

    [Fact]
    public async Task ReplaceFaultedAsync_FactoryThrows_ShouldRaiseNoNotification()
    {
        // Arrange
        var connection =
            new TestTransportConnection(
                TransportConnectionState.Connected);

        var expectedException =
            new InvalidOperationException(
                "Connection creation failed.");

        var factory =
            new TestTransportFactory();

        factory.EnqueueConnection(
            connection);

        factory.EnqueueException(
            expectedException);

        await using var manager =
            new TransportConnectionManager(
                factory);

        await manager.ConnectAsync();

        connection.TransitionTo(
            TransportConnectionState.Faulted);

        var notifications =
            new List<HealthChangedNotification>();

        manager.HealthChanged +=
            (
                sender,
                eventArgs) =>
            {
                notifications.Add(
                    new HealthChangedNotification(
                        sender,
                        eventArgs));
            };

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

        Assert.Empty(
            notifications);

        Assert.Same(
            connection,
            manager.CurrentConnection);

        Assert.Equal(
            TransportConnectionState.Faulted,
            manager.CurrentState);

        Assert.Equal(
            0,
            manager.ReplacementCount);

        Assert.Equal(
            0,
            connection.DisposeCallCount);
    }

    [Fact]
    public async Task DisposeAsync_WithConnection_ShouldRaiseOneNotification()
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
                "The connection timestamp was not established.");

        var notifications =
            new List<HealthChangedNotification>();

        manager.HealthChanged +=
            (
                sender,
                eventArgs) =>
            {
                notifications.Add(
                    new HealthChangedNotification(
                        sender,
                        eventArgs));
            };

        // Act
        await manager.DisposeAsync();

        // Assert
        HealthChangedNotification notification =
            Assert.Single(
                notifications);

        Assert.Same(
            manager,
            notification.Sender);

        Assert.True(
            notification.EventArgs
                .PreviousHealth
                .HasConnection);

        Assert.Equal(
            TransportConnectionState.Connected,
            notification.EventArgs
                .PreviousHealth
                .State);

        Assert.Equal(
            establishedTimestamp,
            notification.EventArgs
                .PreviousHealth
                .LastStateChangeUtc);

        Assert.Equal(
            0,
            notification.EventArgs
                .PreviousHealth
                .ReplacementCount);

        Assert.False(
            notification.EventArgs
                .CurrentHealth
                .HasConnection);

        Assert.Null(
            notification.EventArgs
                .CurrentHealth
                .State);

        Assert.Equal(
            establishedTimestamp,
            notification.EventArgs
                .CurrentHealth
                .LastStateChangeUtc);

        Assert.Equal(
            0,
            notification.EventArgs
                .CurrentHealth
                .ReplacementCount);

        Assert.Equal(
            1,
            connection.DisposeCallCount);

        Assert.Null(
            manager.CurrentConnection);
    }

    [Fact]
    public async Task DisposeAsync_WithoutConnection_ShouldRaiseNoNotification()
    {
        // Arrange
        var factory =
            new TestTransportFactory();

        var manager =
            new TransportConnectionManager(
                factory);

        int notificationCount =
            0;

        manager.HealthChanged +=
            (
                sender,
                eventArgs) =>
            {
                notificationCount++;
            };

        // Act
        await manager.DisposeAsync();

        // Assert
        Assert.Equal(
            0,
            notificationCount);
    }

    [Fact]
    public async Task DisposeAsync_RepeatedCall_ShouldRaiseOneNotification()
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

        int notificationCount =
            0;

        manager.HealthChanged +=
            (
                sender,
                eventArgs) =>
            {
                notificationCount++;
            };

        // Act
        await manager.DisposeAsync();
        await manager.DisposeAsync();

        // Assert
        Assert.Equal(
            1,
            notificationCount);

        Assert.Equal(
            1,
            connection.DisposeCallCount);
    }

    [Fact]
    public async Task PreviousNotificationSnapshots_ShouldRemainImmutable()
    {
        // Arrange
        var firstConnection =
            new TestTransportConnection(
                TransportConnectionState.Connected);

        var secondConnection =
            new TestTransportConnection(
                TransportConnectionState.Connected);

        var factory =
            new TestTransportFactory();

        factory.EnqueueConnection(
            firstConnection);

        factory.EnqueueConnection(
            secondConnection);

        var manager =
            new TransportConnectionManager(
                factory);

        var eventArgsList =
            new List<
                TransportConnectionHealthChangedEventArgs>();

        manager.HealthChanged +=
            (
                sender,
                eventArgs) =>
            {
                eventArgsList.Add(
                    eventArgs);
            };

        await manager.ConnectAsync();

        TransportConnectionHealthChangedEventArgs
            connectionEvent =
                eventArgsList[0];

        firstConnection.TransitionTo(
            TransportConnectionState.Faulted);

        TransportConnectionHealthChangedEventArgs
            faultEvent =
                eventArgsList[1];

        await manager.ReplaceFaultedAsync();

        TransportConnectionHealthChangedEventArgs
            replacementEvent =
                eventArgsList[2];

        await manager.DisposeAsync();

        // Act and Assert
        Assert.Equal(
            4,
            eventArgsList.Count);

        Assert.False(
            connectionEvent
                .PreviousHealth
                .HasConnection);

        Assert.True(
            connectionEvent
                .CurrentHealth
                .HasConnection);

        Assert.Equal(
            TransportConnectionState.Connected,
            connectionEvent
                .CurrentHealth
                .State);

        Assert.Equal(
            0,
            connectionEvent
                .CurrentHealth
                .ReplacementCount);

        Assert.Equal(
            TransportConnectionState.Connected,
            faultEvent
                .PreviousHealth
                .State);

        Assert.Equal(
            TransportConnectionState.Faulted,
            faultEvent
                .CurrentHealth
                .State);

        Assert.Equal(
            0,
            faultEvent
                .CurrentHealth
                .ReplacementCount);

        Assert.Equal(
            TransportConnectionState.Faulted,
            replacementEvent
                .PreviousHealth
                .State);

        Assert.Equal(
            0,
            replacementEvent
                .PreviousHealth
                .ReplacementCount);

        Assert.Equal(
            TransportConnectionState.Connected,
            replacementEvent
                .CurrentHealth
                .State);

        Assert.Equal(
            1,
            replacementEvent
                .CurrentHealth
                .ReplacementCount);
    }

    [Fact]
    public async Task DetachedPreviousConnection_ShouldNotRaiseManagerNotification()
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

        int notificationCount =
            0;

        manager.HealthChanged +=
            (
                sender,
                eventArgs) =>
            {
                notificationCount++;
            };

        // Act
        previousConnection.TransitionTo(
            TransportConnectionState.Connected);

        // Assert
        Assert.Equal(
            0,
            notificationCount);

        Assert.Same(
            replacementConnection,
            manager.CurrentConnection);

        Assert.Equal(
            TransportConnectionState.Connected,
            manager.CurrentState);
    }

    private sealed record HealthChangedNotification(
        object? Sender,
        TransportConnectionHealthChangedEventArgs EventArgs);

    private sealed class TestTransportFactory
        : ITransportFactory
    {
        private readonly Queue<
            Func<ITransportConnection>> _results =
            new();

        public void EnqueueConnection(
            ITransportConnection connection)
        {
            ArgumentNullException.ThrowIfNull(
                connection);

            _results.Enqueue(
                () => connection);
        }

        public void EnqueueException(
            Exception exception)
        {
            ArgumentNullException.ThrowIfNull(
                exception);

            _results.Enqueue(
                () => throw exception);
        }

        public Task<ITransportConnection> ConnectAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_results.Count == 0)
            {
                throw new InvalidOperationException(
                    "No test transport result was configured.");
            }

            ITransportConnection connection =
                _results
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