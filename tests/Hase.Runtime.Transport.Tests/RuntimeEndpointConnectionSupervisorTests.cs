using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class RuntimeEndpointConnectionSupervisorTests
{
    [Fact]
    public async Task RunAsync_ShouldConnectAndUseOnlyOneSupervisionTask()
    {
        var connection =
            new TestTransportConnection();

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
                new DefaultRuntimeEndpointReconnectPolicy());

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task firstSupervisionTask =
            supervisor.RunAsync(
                cancellationTokenSource.Token);

        Task secondSupervisionTask =
            supervisor.RunAsync();

        Assert.Same(
            firstSupervisionTask,
            secondSupervisionTask);

        await synchronizer.SynchronizationCompleted;

        Assert.Equal(
            EndpointConnectionState.Ready,
            runtimeEndpoint.ConnectionStatus.State);

        Assert.Equal(
            1,
            factory.ConnectCallCount);

        Assert.Equal(
            1,
            synchronizer.SynchronizeCallCount);

        Assert.False(
            firstSupervisionTask.IsCompleted);

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await firstSupervisionTask);

        Assert.Equal(
            EndpointConnectionState.Disconnected,
            runtimeEndpoint.ConnectionStatus.State);

        Assert.Equal(
            "Endpoint connection supervision was cancelled.",
            runtimeEndpoint.ConnectionStatus.Detail);

        Assert.Equal(
            1,
            factory.ConnectCallCount);

        Assert.Equal(
            1,
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
        private readonly ITransportConnection _connection;

        public TestTransportFactory(
            ITransportConnection connection)
        {
            _connection =
                connection
                ?? throw new ArgumentNullException(
                    nameof(connection));
        }

        public int ConnectCallCount
        {
            get;
            private set;
        }

        public Task<ITransportConnection> ConnectAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ConnectCallCount++;

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

        public int SynchronizeCallCount
        {
            get;
            private set;
        }

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

            SynchronizeCallCount++;

            _synchronizationCompleted.TrySetResult(
                true);

            return Task.CompletedTask;
        }
    }

    private sealed class TestTransportConnection
        : ITransportConnection
    {
        public event EventHandler<
            TransportConnectionStateChangedEventArgs>?
            StateChanged;

        public TransportConnectionState State =>
            TransportConnectionState.Connected;

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
    }
}