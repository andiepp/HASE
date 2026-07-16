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
        RuntimeEndpoint endpoint =
            CreateRuntimeEndpoint();

        Assert.Throws<ArgumentNullException>(
            () => new RuntimeEndpointConnectionCoordinator(
                null!,
                endpoint));
    }

    [Fact]
    public void Constructor_NullRuntimeEndpoint_ShouldThrow()
    {
        var manager =
            new TransportConnectionManager(
                new TestTransportFactory());

        Assert.Throws<ArgumentNullException>(
            () => new RuntimeEndpointConnectionCoordinator(
                manager,
                null!));
    }

    [Fact]
    public async Task Constructor_ShouldInitializeDisconnectedState()
    {
        await using var manager =
            new TransportConnectionManager(
                new TestTransportFactory());

        RuntimeEndpoint endpoint =
            CreateRuntimeEndpoint();

        await using var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                manager,
                endpoint);

        Assert.Equal(
            EndpointConnectionState.Disconnected,
            endpoint.ConnectionStatus.State);
    }

    [Fact]
    public async Task SuccessfulConnect_ShouldFinishSynchronizing()
    {
        var connection =
            new TestTransportConnection(
                TransportConnectionState.Connected);

        var factory =
            new TestTransportFactory();

        factory.Enqueue(connection);

        await using var manager =
            new TransportConnectionManager(factory);

        RuntimeEndpoint endpoint =
            CreateRuntimeEndpoint();

        await using var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                manager,
                endpoint);

        await coordinator.ConnectAsync();

        Assert.Equal(
            EndpointConnectionState.Synchronizing,
            endpoint.ConnectionStatus.State);
    }

    [Fact]
    public async Task FaultedTransport_ShouldBecomeFaulted()
    {
        var connection =
            new TestTransportConnection(
                TransportConnectionState.Connected);

        var factory =
            new TestTransportFactory();

        factory.Enqueue(connection);

        await using var manager =
            new TransportConnectionManager(factory);

        RuntimeEndpoint endpoint =
            CreateRuntimeEndpoint();

        await using var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                manager,
                endpoint);

        await coordinator.ConnectAsync();

        connection.TransitionTo(
            TransportConnectionState.Faulted);

        Assert.Equal(
            EndpointConnectionState.Faulted,
            endpoint.ConnectionStatus.State);
    }

    private static RuntimeEndpoint CreateRuntimeEndpoint()
    {
        var context =
            new RuntimeContext();

        return context.AddEndpoint(
            new EndpointDescriptor(
                new EndpointId("Endpoint")));
    }

    private sealed class TestTransportFactory
        : ITransportFactory
    {
        private readonly Queue<ITransportConnection> _queue =
            new();

        public void Enqueue(
            ITransportConnection connection)
        {
            _queue.Enqueue(connection);
        }

        public Task<ITransportConnection> ConnectAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_queue.Count == 0)
            {
                throw new InvalidOperationException(
                    "No connection configured.");
            }

            return Task.FromResult(
                _queue.Dequeue());
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
            _state = state;
        }

        public event EventHandler<TransportConnectionStateChangedEventArgs>? StateChanged;

        public TransportConnectionState State => _state;

        public Task<byte[]> ExchangeAsync(
            byte[] request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(request);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public void TransitionTo(
            TransportConnectionState state)
        {
            var previous = _state;
            _state = state;

            StateChanged?.Invoke(
                this,
                new TransportConnectionStateChangedEventArgs(
                    previous,
                    state));
        }
    }
}