using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class RuntimeEndpointConnectionCoordinatorTests
{
    [Fact]
    public void Constructor_NullConnectionManager_ShouldThrow()
    {
        // Arrange
        RuntimeEndpoint endpoint =
            CreateRuntimeEndpoint();

        var synchronizer =
            new TestRuntimeEndpointSynchronizer();

        // Act
        void Act()
        {
            _ = new RuntimeEndpointConnectionCoordinator(
                null!,
                endpoint,
                synchronizer);
        }

        // Assert
        ArgumentNullException exception =
            Assert.Throws<ArgumentNullException>(
                Act);

        Assert.Equal(
            "connectionManager",
            exception.ParamName);
    }

    [Fact]
    public void Constructor_NullRuntimeEndpoint_ShouldThrow()
    {
        // Arrange
        var manager =
            new TransportConnectionManager(
                new TestTransportFactory());

        var synchronizer =
            new TestRuntimeEndpointSynchronizer();

        // Act
        void Act()
        {
            _ = new RuntimeEndpointConnectionCoordinator(
                manager,
                null!,
                synchronizer);
        }

        // Assert
        ArgumentNullException exception =
            Assert.Throws<ArgumentNullException>(
                Act);

        Assert.Equal(
            "runtimeEndpoint",
            exception.ParamName);
    }

    [Fact]
    public void Constructor_NullSynchronizer_ShouldThrow()
    {
        // Arrange
        var manager =
            new TransportConnectionManager(
                new TestTransportFactory());

        RuntimeEndpoint endpoint =
            CreateRuntimeEndpoint();

        // Act
        void Act()
        {
            _ = new RuntimeEndpointConnectionCoordinator(
                manager,
                endpoint,
                null!);
        }

        // Assert
        ArgumentNullException exception =
            Assert.Throws<ArgumentNullException>(
                Act);

        Assert.Equal(
            "synchronizer",
            exception.ParamName);
    }

    [Fact]
    public async Task Constructor_ShouldInitializeDisconnectedState()
    {
        // Arrange
        await using var manager =
            new TransportConnectionManager(
                new TestTransportFactory());

        RuntimeEndpoint endpoint =
            CreateRuntimeEndpoint();

        var synchronizer =
            new TestRuntimeEndpointSynchronizer();

        // Act
        await using var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                manager,
                endpoint,
                synchronizer);

        // Assert
        Assert.Equal(
            EndpointConnectionState.Disconnected,
            endpoint.ConnectionStatus.State);

        Assert.Null(
            endpoint.ConnectionStatus.ChangedAtUtc);

        Assert.Equal(
            "No transport connection is currently available.",
            endpoint.ConnectionStatus.Detail);
    }

    [Fact]
    public async Task Constructor_ShouldExposeDependencies()
    {
        // Arrange
        await using var manager =
            new TransportConnectionManager(
                new TestTransportFactory());

        RuntimeEndpoint endpoint =
            CreateRuntimeEndpoint();

        var synchronizer =
            new TestRuntimeEndpointSynchronizer();

        // Act
        await using var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                manager,
                endpoint,
                synchronizer);

        // Assert
        Assert.Same(
            manager,
            coordinator.ConnectionManager);

        Assert.Same(
            endpoint,
            coordinator.RuntimeEndpoint);

        Assert.Same(
            synchronizer,
            coordinator.Synchronizer);
    }

    [Fact]
    public async Task ConnectAsync_WhileFactoryIsPending_ShouldReportConnecting()
    {
        // Arrange
        var connection =
            new TestTransportConnection(
                TransportConnectionState.Connected);

        var pendingConnection =
            new TaskCompletionSource<ITransportConnection>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var factory =
            new TestTransportFactory();

        factory.EnqueuePendingConnection(
            pendingConnection);

        await using var manager =
            new TransportConnectionManager(
                factory);

        RuntimeEndpoint endpoint =
            CreateRuntimeEndpoint();

        var synchronizer =
            new TestRuntimeEndpointSynchronizer();

        await using var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                manager,
                endpoint,
                synchronizer);

        // Act
        Task<ITransportConnection> connectTask =
            coordinator.ConnectAsync();

        await factory.ConnectStarted;

        // Assert
        Assert.Equal(
            EndpointConnectionState.Connecting,
            endpoint.ConnectionStatus.State);

        Assert.NotNull(
            endpoint.ConnectionStatus.ChangedAtUtc);

        Assert.Equal(
            TimeSpan.Zero,
            endpoint.ConnectionStatus
                .ChangedAtUtc
                .Value
                .Offset);

        Assert.Equal(
            "Establishing the transport connection.",
            endpoint.ConnectionStatus.Detail);

        Assert.False(
            connectTask.IsCompleted);

        pendingConnection.SetResult(
            connection);

        ITransportConnection actualConnection =
            await connectTask;

        Assert.Same(
            connection,
            actualConnection);

        Assert.Equal(
            EndpointConnectionState.Ready,
            endpoint.ConnectionStatus.State);
    }

    [Fact]
    public async Task ConnectAsync_WhileSynchronizationIsPending_ShouldReportSynchronizing()
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

        RuntimeEndpoint endpoint =
            CreateRuntimeEndpoint();

        var pendingSynchronization =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var synchronizer =
            new TestRuntimeEndpointSynchronizer();

        synchronizer.EnqueuePendingSynchronization(
            pendingSynchronization);

        await using var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                manager,
                endpoint,
                synchronizer);

        // Act
        Task<ITransportConnection> connectTask =
            coordinator.ConnectAsync();

        await synchronizer.SynchronizationStarted;

        // Assert
        Assert.Equal(
            EndpointConnectionState.Synchronizing,
            endpoint.ConnectionStatus.State);

        Assert.Equal(
            manager.LastStateChangeUtc,
            endpoint.ConnectionStatus.ChangedAtUtc);

        Assert.Equal(
            "Synchronizing the runtime endpoint with the physical endpoint.",
            endpoint.ConnectionStatus.Detail);

        Assert.False(
            connectTask.IsCompleted);

        pendingSynchronization.SetResult(
            true);

        ITransportConnection actualConnection =
            await connectTask;

        Assert.Same(
            connection,
            actualConnection);

        Assert.Equal(
            EndpointConnectionState.Ready,
            endpoint.ConnectionStatus.State);
    }

    [Fact]
    public async Task SuccessfulConnect_ShouldFinishReady()
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

        RuntimeEndpoint endpoint =
            CreateRuntimeEndpoint();

        var synchronizer =
            new TestRuntimeEndpointSynchronizer();

        await using var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                manager,
                endpoint,
                synchronizer);

        // Act
        ITransportConnection actualConnection =
            await coordinator.ConnectAsync();

        // Assert
        Assert.Same(
            connection,
            actualConnection);

        Assert.Equal(
            EndpointConnectionState.Ready,
            endpoint.ConnectionStatus.State);

        Assert.NotNull(
            endpoint.ConnectionStatus.ChangedAtUtc);

        Assert.Equal(
            TimeSpan.Zero,
            endpoint.ConnectionStatus
                .ChangedAtUtc
                .Value
                .Offset);

        Assert.Equal(
            "The endpoint is connected, synchronized, and ready.",
            endpoint.ConnectionStatus.Detail);

        Assert.Equal(
            1,
            synchronizer.SynchronizeCallCount);
    }

    [Fact]
    public async Task SuccessfulConnect_ShouldPassConnectionAndEndpointToSynchronizer()
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

        RuntimeEndpoint endpoint =
            CreateRuntimeEndpoint();

        var synchronizer =
            new TestRuntimeEndpointSynchronizer();

        await using var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                manager,
                endpoint,
                synchronizer);

        // Act
        await coordinator.ConnectAsync();

        // Assert
        Assert.Same(
            connection,
            synchronizer.LastConnection);

        Assert.Same(
            endpoint,
            synchronizer.LastRuntimeEndpoint);

        Assert.Equal(
            1,
            synchronizer.SynchronizeCallCount);
    }

    [Fact]
    public async Task SuccessfulConnect_ShouldPassCancellationTokenToSynchronizer()
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

        RuntimeEndpoint endpoint =
            CreateRuntimeEndpoint();

        var synchronizer =
            new TestRuntimeEndpointSynchronizer();

        await using var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                manager,
                endpoint,
                synchronizer);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        // Act
        await coordinator.ConnectAsync(
            cancellationTokenSource.Token);

        // Assert
        Assert.Equal(
            cancellationTokenSource.Token,
            synchronizer.LastCancellationToken);
    }

    [Fact]
    public async Task ConnectAsync_CancelledDuringConnection_ShouldReturnToDisconnected()
    {
        // Arrange
        var factory =
            new TestTransportFactory();

        factory.EnqueueCancellation();

        await using var manager =
            new TransportConnectionManager(
                factory);

        RuntimeEndpoint endpoint =
            CreateRuntimeEndpoint();

        var synchronizer =
            new TestRuntimeEndpointSynchronizer();

        await using var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                manager,
                endpoint,
                synchronizer);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task<ITransportConnection> connectTask =
            coordinator.ConnectAsync(
                cancellationTokenSource.Token);

        await factory.ConnectStarted;

        Assert.Equal(
            EndpointConnectionState.Connecting,
            endpoint.ConnectionStatus.State);

        // Act
        cancellationTokenSource.Cancel();

        // Assert
        TaskCanceledException exception =
            await Assert.ThrowsAsync<TaskCanceledException>(
                async () =>
                {
                    await connectTask;
                });

        Assert.Equal(
            cancellationTokenSource.Token,
            exception.CancellationToken);

        Assert.Equal(
            EndpointConnectionState.Disconnected,
            endpoint.ConnectionStatus.State);

        Assert.NotNull(
            endpoint.ConnectionStatus.ChangedAtUtc);

        Assert.Equal(
            TimeSpan.Zero,
            endpoint.ConnectionStatus
                .ChangedAtUtc
                .Value
                .Offset);

        Assert.Equal(
            "The transport connection attempt was cancelled.",
            endpoint.ConnectionStatus.Detail);

        Assert.Null(
            manager.CurrentConnection);

        Assert.Equal(
            0,
            synchronizer.SynchronizeCallCount);
    }

    [Fact]
    public async Task ConnectAsync_ConnectionFailure_ShouldBecomeFaulted()
    {
        // Arrange
        var expectedException =
            new InvalidOperationException(
                "Connection creation failed.");

        var factory =
            new TestTransportFactory();

        factory.EnqueueException(
            expectedException);

        await using var manager =
            new TransportConnectionManager(
                factory);

        RuntimeEndpoint endpoint =
            CreateRuntimeEndpoint();

        var synchronizer =
            new TestRuntimeEndpointSynchronizer();

        await using var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                manager,
                endpoint,
                synchronizer);

        // Act
        Task Act()
        {
            return coordinator.ConnectAsync();
        }

        // Assert
        InvalidOperationException actualException =
            await Assert.ThrowsAsync<InvalidOperationException>(
                Act);

        Assert.Same(
            expectedException,
            actualException);

        Assert.Equal(
            EndpointConnectionState.Faulted,
            endpoint.ConnectionStatus.State);

        Assert.NotNull(
            endpoint.ConnectionStatus.ChangedAtUtc);

        Assert.Equal(
            TimeSpan.Zero,
            endpoint.ConnectionStatus
                .ChangedAtUtc
                .Value
                .Offset);

        Assert.Equal(
            "The transport connection attempt failed.",
            endpoint.ConnectionStatus.Detail);

        Assert.Null(
            manager.CurrentConnection);

        Assert.Equal(
            0,
            synchronizer.SynchronizeCallCount);
    }

    [Fact]
    public async Task ConnectAsync_SynchronizationCancelled_ShouldBecomeDisconnected()
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

        RuntimeEndpoint endpoint =
            CreateRuntimeEndpoint();

        var synchronizer =
            new TestRuntimeEndpointSynchronizer();

        synchronizer.EnqueueCancellation();

        await using var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                manager,
                endpoint,
                synchronizer);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task<ITransportConnection> connectTask =
            coordinator.ConnectAsync(
                cancellationTokenSource.Token);

        await synchronizer.SynchronizationStarted;

        Assert.Equal(
            EndpointConnectionState.Synchronizing,
            endpoint.ConnectionStatus.State);

        // Act
        cancellationTokenSource.Cancel();

        // Assert
        TaskCanceledException exception =
            await Assert.ThrowsAsync<TaskCanceledException>(
                async () =>
                {
                    await connectTask;
                });

        Assert.Equal(
            cancellationTokenSource.Token,
            exception.CancellationToken);

        Assert.Equal(
            EndpointConnectionState.Disconnected,
            endpoint.ConnectionStatus.State);

        Assert.NotNull(
            endpoint.ConnectionStatus.ChangedAtUtc);

        Assert.Equal(
            TimeSpan.Zero,
            endpoint.ConnectionStatus
                .ChangedAtUtc
                .Value
                .Offset);

        Assert.Equal(
            "Endpoint synchronization was cancelled.",
            endpoint.ConnectionStatus.Detail);

        Assert.Same(
            connection,
            manager.CurrentConnection);

        Assert.Equal(
            TransportConnectionState.Connected,
            manager.CurrentState);

        Assert.Equal(
            1,
            synchronizer.SynchronizeCallCount);
    }

    [Fact]
    public async Task ConnectAsync_SynchronizationFailure_ShouldBecomeFaulted()
    {
        // Arrange
        var connection =
            new TestTransportConnection(
                TransportConnectionState.Connected);

        var expectedException =
            new InvalidOperationException(
                "Synchronization failed.");

        var factory =
            new TestTransportFactory();

        factory.EnqueueConnection(
            connection);

        await using var manager =
            new TransportConnectionManager(
                factory);

        RuntimeEndpoint endpoint =
            CreateRuntimeEndpoint();

        var synchronizer =
            new TestRuntimeEndpointSynchronizer();

        synchronizer.EnqueueException(
            expectedException);

        await using var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                manager,
                endpoint,
                synchronizer);

        // Act
        Task Act()
        {
            return coordinator.ConnectAsync();
        }

        // Assert
        InvalidOperationException actualException =
            await Assert.ThrowsAsync<InvalidOperationException>(
                Act);

        Assert.Same(
            expectedException,
            actualException);

        Assert.Equal(
            EndpointConnectionState.Faulted,
            endpoint.ConnectionStatus.State);

        Assert.NotNull(
            endpoint.ConnectionStatus.ChangedAtUtc);

        Assert.Equal(
            TimeSpan.Zero,
            endpoint.ConnectionStatus
                .ChangedAtUtc
                .Value
                .Offset);

        Assert.Equal(
            "Endpoint synchronization failed.",
            endpoint.ConnectionStatus.Detail);

        Assert.Same(
            connection,
            manager.CurrentConnection);

        Assert.Equal(
            TransportConnectionState.Connected,
            manager.CurrentState);

        Assert.Equal(
            1,
            synchronizer.SynchronizeCallCount);
    }

    [Fact]
    public async Task FaultedTransport_AfterReady_ShouldBecomeFaulted()
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

        RuntimeEndpoint endpoint =
            CreateRuntimeEndpoint();

        var synchronizer =
            new TestRuntimeEndpointSynchronizer();

        await using var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                manager,
                endpoint,
                synchronizer);

        await coordinator.ConnectAsync();

        DateTimeOffset readyTimestamp =
            endpoint.ConnectionStatus.ChangedAtUtc
            ?? throw new InvalidOperationException(
                "The ready timestamp was not established.");

        await Task.Delay(
            TimeSpan.FromMilliseconds(
                20));

        // Act
        connection.TransitionTo(
            TransportConnectionState.Faulted);

        // Assert
        Assert.Equal(
            EndpointConnectionState.Faulted,
            endpoint.ConnectionStatus.State);

        Assert.NotNull(
            endpoint.ConnectionStatus.ChangedAtUtc);

        Assert.True(
            endpoint.ConnectionStatus.ChangedAtUtc.Value
            > readyTimestamp);

        Assert.Equal(
            manager.LastStateChangeUtc,
            endpoint.ConnectionStatus.ChangedAtUtc);

        Assert.Equal(
            "The transport connection faulted and cannot be reused.",
            endpoint.ConnectionStatus.Detail);
    }

    [Fact]
    public async Task ConnectionManagerDisposal_AfterReady_ShouldBecomeDisconnected()
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

        RuntimeEndpoint endpoint =
            CreateRuntimeEndpoint();

        var synchronizer =
            new TestRuntimeEndpointSynchronizer();

        await using var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                manager,
                endpoint,
                synchronizer);

        await coordinator.ConnectAsync();

        DateTimeOffset readyTimestamp =
            endpoint.ConnectionStatus.ChangedAtUtc
            ?? throw new InvalidOperationException(
                "The ready timestamp was not established.");

        // Act
        await manager.DisposeAsync();

        // Assert
        Assert.Equal(
            EndpointConnectionState.Disconnected,
            endpoint.ConnectionStatus.State);

        Assert.Equal(
            manager.LastStateChangeUtc,
            endpoint.ConnectionStatus.ChangedAtUtc);

        Assert.NotEqual(
            readyTimestamp,
            endpoint.ConnectionStatus.ChangedAtUtc);

        Assert.Equal(
            "No transport connection is currently available.",
            endpoint.ConnectionStatus.Detail);

        Assert.Null(
            manager.CurrentConnection);

        Assert.Equal(
            1,
            connection.DisposeCallCount);
    }

    [Fact]
    public async Task CoordinatorDisposal_ShouldStopFurtherStatusUpdates()
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

        RuntimeEndpoint endpoint =
            CreateRuntimeEndpoint();

        var synchronizer =
            new TestRuntimeEndpointSynchronizer();

        var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                manager,
                endpoint,
                synchronizer);

        await coordinator.ConnectAsync();

        EndpointConnectionStatus statusBeforeDisposal =
            endpoint.ConnectionStatus;

        await coordinator.DisposeAsync();

        // Act
        connection.TransitionTo(
            TransportConnectionState.Faulted);

        // Assert
        Assert.Same(
            statusBeforeDisposal,
            endpoint.ConnectionStatus);

        Assert.Equal(
            EndpointConnectionState.Ready,
            endpoint.ConnectionStatus.State);

        Assert.Equal(
            TransportConnectionState.Faulted,
            manager.CurrentState);
    }

    [Fact]
    public async Task CoordinatorDisposal_RepeatedCall_ShouldRemainHarmless()
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

        RuntimeEndpoint endpoint =
            CreateRuntimeEndpoint();

        var synchronizer =
            new TestRuntimeEndpointSynchronizer();

        var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                manager,
                endpoint,
                synchronizer);

        await coordinator.ConnectAsync();

        EndpointConnectionStatus statusBeforeDisposal =
            endpoint.ConnectionStatus;

        // Act
        await coordinator.DisposeAsync();
        await coordinator.DisposeAsync();

        connection.TransitionTo(
            TransportConnectionState.Faulted);

        // Assert
        Assert.Same(
            statusBeforeDisposal,
            endpoint.ConnectionStatus);

        Assert.Equal(
            EndpointConnectionState.Ready,
            endpoint.ConnectionStatus.State);
    }

    [Fact]
    public async Task ConnectAsync_AfterCoordinatorDisposal_ShouldThrow()
    {
        // Arrange
        var factory =
            new TestTransportFactory();

        await using var manager =
            new TransportConnectionManager(
                factory);

        RuntimeEndpoint endpoint =
            CreateRuntimeEndpoint();

        var synchronizer =
            new TestRuntimeEndpointSynchronizer();

        var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                manager,
                endpoint,
                synchronizer);

        await coordinator.DisposeAsync();

        // Act
        Task Act()
        {
            return coordinator.ConnectAsync();
        }

        // Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(
            Act);

        Assert.Equal(
            EndpointConnectionState.Disconnected,
            endpoint.ConnectionStatus.State);

        Assert.Equal(
            0,
            factory.ConnectCallCount);

        Assert.Equal(
            0,
            synchronizer.SynchronizeCallCount);
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

    private sealed class TestTransportFactory
        : ITransportFactory
    {
        private readonly Queue<
            Func<
                CancellationToken,
                Task<ITransportConnection>>> _results =
            new();

        private readonly TaskCompletionSource<bool>
            _connectStarted =
                new(
                    TaskCreationOptions.RunContinuationsAsynchronously);

        public int ConnectCallCount
        {
            get;
            private set;
        }

        public Task ConnectStarted =>
            _connectStarted.Task;

        public void EnqueueConnection(
            ITransportConnection connection)
        {
            ArgumentNullException.ThrowIfNull(
                connection);

            _results.Enqueue(
                cancellationToken =>
                    Task.FromResult(
                        connection));
        }

        public void EnqueuePendingConnection(
            TaskCompletionSource<ITransportConnection>
                pendingConnection)
        {
            ArgumentNullException.ThrowIfNull(
                pendingConnection);

            _results.Enqueue(
                cancellationToken =>
                    pendingConnection.Task);
        }

        public void EnqueueCancellation()
        {
            _results.Enqueue(
                async cancellationToken =>
                {
                    await Task.Delay(
                        Timeout.InfiniteTimeSpan,
                        cancellationToken);

                    throw new InvalidOperationException(
                        "The cancellation wait completed unexpectedly.");
                });
        }

        public void EnqueueException(
            Exception exception)
        {
            ArgumentNullException.ThrowIfNull(
                exception);

            _results.Enqueue(
                cancellationToken =>
                    Task.FromException<ITransportConnection>(
                        exception));
        }

        public Task<ITransportConnection> ConnectAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ConnectCallCount++;

            _connectStarted.TrySetResult(
                true);

            if (_results.Count == 0)
            {
                throw new InvalidOperationException(
                    "No connection result was configured.");
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
        private readonly Queue<
            Func<CancellationToken, Task>> _results =
            new();

        private readonly TaskCompletionSource<bool>
            _synchronizationStarted =
                new(
                    TaskCreationOptions.RunContinuationsAsynchronously);

        public int SynchronizeCallCount
        {
            get;
            private set;
        }

        public Task SynchronizationStarted =>
            _synchronizationStarted.Task;

        public ITransportConnection? LastConnection
        {
            get;
            private set;
        }

        public RuntimeEndpoint? LastRuntimeEndpoint
        {
            get;
            private set;
        }

        public CancellationToken LastCancellationToken
        {
            get;
            private set;
        }

        public void EnqueuePendingSynchronization(
            TaskCompletionSource<bool> pendingSynchronization)
        {
            ArgumentNullException.ThrowIfNull(
                pendingSynchronization);

            _results.Enqueue(
                cancellationToken =>
                    pendingSynchronization.Task);
        }

        public void EnqueueCancellation()
        {
            _results.Enqueue(
                async cancellationToken =>
                {
                    await Task.Delay(
                        Timeout.InfiniteTimeSpan,
                        cancellationToken);
                });
        }

        public void EnqueueException(
            Exception exception)
        {
            ArgumentNullException.ThrowIfNull(
                exception);

            _results.Enqueue(
                cancellationToken =>
                    Task.FromException(
                        exception));
        }

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

            SynchronizeCallCount++;

            LastConnection =
                connection;

            LastRuntimeEndpoint =
                runtimeEndpoint;

            LastCancellationToken =
                cancellationToken;

            _synchronizationStarted.TrySetResult(
                true);

            if (_results.Count == 0)
            {
                return Task.CompletedTask;
            }

            Func<CancellationToken, Task> result =
                _results.Dequeue();

            return result(
                cancellationToken);
        }
    }

    private sealed class TestTransportConnection
        : ITransportConnection,
          IAsyncDisposable
    {
        private TransportConnectionState _state;

        public TestTransportConnection(
            TransportConnectionState state)
        {
            _state =
                state;
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