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

        // Act
        void Act()
        {
            _ = new RuntimeEndpointConnectionCoordinator(
                null!,
                endpoint);
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

        // Act
        void Act()
        {
            _ = new RuntimeEndpointConnectionCoordinator(
                manager,
                null!);
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
    public async Task Constructor_ShouldInitializeDisconnectedState()
    {
        // Arrange
        await using var manager =
            new TransportConnectionManager(
                new TestTransportFactory());

        RuntimeEndpoint endpoint =
            CreateRuntimeEndpoint();

        // Act
        await using var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                manager,
                endpoint);

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

        await using var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                manager,
                endpoint);

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
            EndpointConnectionState.Synchronizing,
            endpoint.ConnectionStatus.State);
    }

    [Fact]
    public async Task SuccessfulConnect_ShouldFinishSynchronizing()
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

        await using var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                manager,
                endpoint);

        // Act
        ITransportConnection actualConnection =
            await coordinator.ConnectAsync();

        // Assert
        Assert.Same(
            connection,
            actualConnection);

        Assert.Equal(
            EndpointConnectionState.Synchronizing,
            endpoint.ConnectionStatus.State);

        Assert.Equal(
            manager.LastStateChangeUtc,
            endpoint.ConnectionStatus.ChangedAtUtc);

        Assert.Equal(
            "The transport connection is established; "
            + "endpoint synchronization is required.",
            endpoint.ConnectionStatus.Detail);
    }

    [Fact]
    public async Task SuccessfulConnect_ShouldPreserveTransportTimestamp()
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

        await using var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                manager,
                endpoint);

        DateTimeOffset beforeConnectUtc =
            DateTimeOffset.UtcNow;

        // Act
        await coordinator.ConnectAsync();

        DateTimeOffset afterConnectUtc =
            DateTimeOffset.UtcNow;

        // Assert
        Assert.NotNull(
            manager.LastStateChangeUtc);

        Assert.NotNull(
            endpoint.ConnectionStatus.ChangedAtUtc);

        Assert.Equal(
            manager.LastStateChangeUtc,
            endpoint.ConnectionStatus.ChangedAtUtc);

        Assert.InRange(
            endpoint.ConnectionStatus.ChangedAtUtc.Value,
            beforeConnectUtc,
            afterConnectUtc);

        Assert.Equal(
            TimeSpan.Zero,
            endpoint.ConnectionStatus
                .ChangedAtUtc
                .Value
                .Offset);
    }

    [Fact]
    public async Task ConnectAsync_Cancelled_ShouldReturnToDisconnected()
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

        await using var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                manager,
                endpoint);

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
    }

    [Fact]
    public async Task ConnectAsync_FactoryFailure_ShouldBecomeFaulted()
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

        await using var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                manager,
                endpoint);

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
    }

    [Fact]
    public async Task FaultedTransport_ShouldBecomeFaulted()
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

        await using var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                manager,
                endpoint);

        await coordinator.ConnectAsync();

        DateTimeOffset synchronizingTimestamp =
            endpoint.ConnectionStatus.ChangedAtUtc
            ?? throw new InvalidOperationException(
                "The synchronizing timestamp was not established.");

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
            > synchronizingTimestamp);

        Assert.Equal(
            manager.LastStateChangeUtc,
            endpoint.ConnectionStatus.ChangedAtUtc);

        Assert.Equal(
            "The transport connection faulted and cannot be reused.",
            endpoint.ConnectionStatus.Detail);
    }

    [Fact]
    public async Task ConnectionManagerDisposal_ShouldBecomeDisconnected()
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

        await using var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                manager,
                endpoint);

        await coordinator.ConnectAsync();

        DateTimeOffset establishedTimestamp =
            endpoint.ConnectionStatus.ChangedAtUtc
            ?? throw new InvalidOperationException(
                "The synchronizing timestamp was not established.");

        // Act
        await manager.DisposeAsync();

        // Assert
        Assert.Equal(
            EndpointConnectionState.Disconnected,
            endpoint.ConnectionStatus.State);

        Assert.Equal(
            establishedTimestamp,
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

        var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                manager,
                endpoint);

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
            EndpointConnectionState.Synchronizing,
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

        var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                manager,
                endpoint);

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
            EndpointConnectionState.Synchronizing,
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

        var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                manager,
                endpoint);

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

        private TaskCompletionSource<bool>
            _connectStarted =
                CreateConnectStartedSource();

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

        private static TaskCompletionSource<bool>
            CreateConnectStartedSource()
        {
            return new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
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