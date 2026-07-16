using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class RuntimeTransportConnectionBridgeTests
{
    [Fact]
    public void Constructor_NullConnectionManager_ShouldThrow()
    {
        // Arrange
        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint();

        // Act
        void Act()
        {
            _ = new RuntimeTransportConnectionBridge(
                null!,
                runtimeEndpoint);
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
        var factory =
            new TestTransportFactory();

        var connectionManager =
            new TransportConnectionManager(
                factory);

        // Act
        void Act()
        {
            _ = new RuntimeTransportConnectionBridge(
                connectionManager,
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
    public async Task Constructor_WithoutTransportConnection_ShouldMapDisconnected()
    {
        // Arrange
        var factory =
            new TestTransportFactory();

        await using var connectionManager =
            new TransportConnectionManager(
                factory);

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint();

        // Act
        using var bridge =
            new RuntimeTransportConnectionBridge(
                connectionManager,
                runtimeEndpoint);

        // Assert
        Assert.Equal(
            EndpointConnectionState.Disconnected,
            runtimeEndpoint.ConnectionStatus.State);

        Assert.Null(
            runtimeEndpoint.ConnectionStatus.ChangedAtUtc);

        Assert.Equal(
            "No transport connection is currently available.",
            runtimeEndpoint.ConnectionStatus.Detail);
    }

    [Fact]
    public async Task ConnectedTransport_ShouldMapReady()
    {
        // Arrange
        var connection =
            new TestTransportConnection(
                TransportConnectionState.Connected);

        var factory =
            new TestTransportFactory();

        factory.EnqueueConnection(
            connection);

        await using var connectionManager =
            new TransportConnectionManager(
                factory);

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint();

        using var bridge =
            new RuntimeTransportConnectionBridge(
                connectionManager,
                runtimeEndpoint);

        DateTimeOffset beforeConnectUtc =
            DateTimeOffset.UtcNow;

        // Act
        await connectionManager.ConnectAsync();

        DateTimeOffset afterConnectUtc =
            DateTimeOffset.UtcNow;

        // Assert
        Assert.Equal(
            EndpointConnectionState.Ready,
            runtimeEndpoint.ConnectionStatus.State);

        Assert.NotNull(
            runtimeEndpoint.ConnectionStatus.ChangedAtUtc);

        Assert.InRange(
            runtimeEndpoint.ConnectionStatus.ChangedAtUtc.Value,
            beforeConnectUtc,
            afterConnectUtc);

        Assert.Equal(
            connectionManager.LastStateChangeUtc,
            runtimeEndpoint.ConnectionStatus.ChangedAtUtc);

        Assert.Equal(
            "The transport connection is available.",
            runtimeEndpoint.ConnectionStatus.Detail);
    }

    [Fact]
    public async Task FaultedTransport_ShouldMapFaulted()
    {
        // Arrange
        var connection =
            new TestTransportConnection(
                TransportConnectionState.Connected);

        var factory =
            new TestTransportFactory();

        factory.EnqueueConnection(
            connection);

        await using var connectionManager =
            new TransportConnectionManager(
                factory);

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint();

        using var bridge =
            new RuntimeTransportConnectionBridge(
                connectionManager,
                runtimeEndpoint);

        await connectionManager.ConnectAsync();

        DateTimeOffset readyTimestamp =
            runtimeEndpoint.ConnectionStatus.ChangedAtUtc
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
            runtimeEndpoint.ConnectionStatus.State);

        Assert.NotNull(
            runtimeEndpoint.ConnectionStatus.ChangedAtUtc);

        Assert.True(
            runtimeEndpoint.ConnectionStatus.ChangedAtUtc.Value
            > readyTimestamp);

        Assert.Equal(
            connectionManager.LastStateChangeUtc,
            runtimeEndpoint.ConnectionStatus.ChangedAtUtc);

        Assert.Equal(
            "The transport connection faulted and cannot be reused.",
            runtimeEndpoint.ConnectionStatus.Detail);
    }

    [Fact]
    public async Task ConnectionManagerDisposal_ShouldMapDisconnected()
    {
        // Arrange
        var connection =
            new TestTransportConnection(
                TransportConnectionState.Connected);

        var factory =
            new TestTransportFactory();

        factory.EnqueueConnection(
            connection);

        var connectionManager =
            new TransportConnectionManager(
                factory);

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint();

        using var bridge =
            new RuntimeTransportConnectionBridge(
                connectionManager,
                runtimeEndpoint);

        await connectionManager.ConnectAsync();

        DateTimeOffset establishedTimestamp =
            runtimeEndpoint.ConnectionStatus.ChangedAtUtc
            ?? throw new InvalidOperationException(
                "The ready timestamp was not established.");

        // Act
        await connectionManager.DisposeAsync();

        // Assert
        Assert.Equal(
            EndpointConnectionState.Disconnected,
            runtimeEndpoint.ConnectionStatus.State);

        Assert.Equal(
            establishedTimestamp,
            runtimeEndpoint.ConnectionStatus.ChangedAtUtc);

        Assert.Equal(
            "No transport connection is currently available.",
            runtimeEndpoint.ConnectionStatus.Detail);

        Assert.Null(
            connectionManager.CurrentConnection);

        Assert.Equal(
            1,
            connection.DisposeCallCount);
    }

    [Fact]
    public async Task BridgeDisposal_ShouldStopFurtherStatusUpdates()
    {
        // Arrange
        var connection =
            new TestTransportConnection(
                TransportConnectionState.Connected);

        var factory =
            new TestTransportFactory();

        factory.EnqueueConnection(
            connection);

        await using var connectionManager =
            new TransportConnectionManager(
                factory);

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint();

        var bridge =
            new RuntimeTransportConnectionBridge(
                connectionManager,
                runtimeEndpoint);

        await connectionManager.ConnectAsync();

        EndpointConnectionStatus statusBeforeDisposal =
            runtimeEndpoint.ConnectionStatus;

        bridge.Dispose();

        // Act
        connection.TransitionTo(
            TransportConnectionState.Faulted);

        // Assert
        Assert.Same(
            statusBeforeDisposal,
            runtimeEndpoint.ConnectionStatus);

        Assert.Equal(
            EndpointConnectionState.Ready,
            runtimeEndpoint.ConnectionStatus.State);

        Assert.Equal(
            TransportConnectionState.Faulted,
            connectionManager.CurrentState);
    }

    [Fact]
    public async Task BridgeDisposal_RepeatedCall_ShouldRemainHarmless()
    {
        // Arrange
        var connection =
            new TestTransportConnection(
                TransportConnectionState.Connected);

        var factory =
            new TestTransportFactory();

        factory.EnqueueConnection(
            connection);

        await using var connectionManager =
            new TransportConnectionManager(
                factory);

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint();

        var bridge =
            new RuntimeTransportConnectionBridge(
                connectionManager,
                runtimeEndpoint);

        await connectionManager.ConnectAsync();

        EndpointConnectionStatus statusBeforeDisposal =
            runtimeEndpoint.ConnectionStatus;

        // Act
        bridge.Dispose();
        bridge.Dispose();

        connection.TransitionTo(
            TransportConnectionState.Faulted);

        // Assert
        Assert.Same(
            statusBeforeDisposal,
            runtimeEndpoint.ConnectionStatus);

        Assert.Equal(
            EndpointConnectionState.Ready,
            runtimeEndpoint.ConnectionStatus.State);

        Assert.Equal(
            TransportConnectionState.Faulted,
            connectionManager.CurrentState);
    }

    private static RuntimeEndpoint CreateRuntimeEndpoint()
    {
        var context =
            new RuntimeContext();

        var descriptor =
            new EndpointDescriptor(
                new EndpointId(
                    "test-endpoint"));

        return context.AddEndpoint(
            descriptor);
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