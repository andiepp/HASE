using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Protocol;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class RuntimeEndpointConnectionCoordinatorHealthProbeTests
{
    [Fact]
    public async Task ProbeAsync_ResponseTimeout_ShouldFaultTransportAndRuntimeEndpoint()
    {
        // Arrange
        var transportConnection =
            new NonRespondingDuplexTransportConnection();

        var transportFactory =
            new TestTransportFactory(
                transportConnection);

        await using var connectionManager =
            new TransportConnectionManager(
                transportFactory);

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint();

        var synchronizer =
            new TestProtocolSynchronizer();

        await using var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                connectionManager,
                runtimeEndpoint,
                synchronizer);

        await coordinator.ConnectAsync();

        var request =
            CreateRequest(
                14_001);

        // Act
        Task Act()
        {
            return coordinator.ProbeAsync(
                request,
                TimeSpan.FromMilliseconds(
                    50));
        }

        // Assert
        await Assert.ThrowsAsync<TimeoutException>(
            Act);

        Assert.Equal(
            1,
            transportConnection.SendCount);

        Assert.Equal(
            TransportConnectionState.Faulted,
            transportConnection.State);

        Assert.Equal(
            EndpointConnectionState.Faulted,
            runtimeEndpoint.ConnectionStatus.State);
    }

    [Fact]
    public async Task ProbeAsync_ZeroTimeout_ShouldRejectWithoutFaultingTransport()
    {
        // Arrange
        var transportConnection =
            new NonRespondingDuplexTransportConnection();

        var transportFactory =
            new TestTransportFactory(
                transportConnection);

        await using var connectionManager =
            new TransportConnectionManager(
                transportFactory);

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint();

        var synchronizer =
            new TestProtocolSynchronizer();

        await using var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                connectionManager,
                runtimeEndpoint,
                synchronizer);

        await coordinator.ConnectAsync();

        var request =
            CreateRequest(
                14_002);

        // Act
        Task Act()
        {
            return coordinator.ProbeAsync(
                request,
                TimeSpan.Zero);
        }

        // Assert
        ArgumentOutOfRangeException exception =
            await Assert.ThrowsAsync<
                ArgumentOutOfRangeException>(
                    Act);

        Assert.Equal(
            "timeout",
            exception.ParamName);

        Assert.Equal(
            0,
            transportConnection.SendCount);

        Assert.Equal(
            TransportConnectionState.Connected,
            transportConnection.State);

        Assert.Equal(
            EndpointConnectionState.Ready,
            runtimeEndpoint.ConnectionStatus.State);
    }

    [Fact]
    public async Task ProbeAsync_CallerCancellation_ShouldNotFaultTransport()
    {
        // Arrange
        var transportConnection =
            new NonRespondingDuplexTransportConnection();

        var transportFactory =
            new TestTransportFactory(
                transportConnection);

        await using var connectionManager =
            new TransportConnectionManager(
                transportFactory);

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint();

        var synchronizer =
            new TestProtocolSynchronizer();

        await using var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                connectionManager,
                runtimeEndpoint,
                synchronizer);

        await coordinator.ConnectAsync();

        var request =
            CreateRequest(
                14_003);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.CancelAfter(
            TimeSpan.FromMilliseconds(
                50));

        // Act
        Task Act()
        {
            return coordinator.ProbeAsync(
                request,
                Timeout.InfiniteTimeSpan,
                cancellationTokenSource.Token);
        }

        // Assert
        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(
                Act);

        Assert.Equal(
            1,
            transportConnection.SendCount);

        Assert.Equal(
            TransportConnectionState.Connected,
            transportConnection.State);

        Assert.Equal(
            EndpointConnectionState.Ready,
            runtimeEndpoint.ConnectionStatus.State);
    }

    private static DiscoverRequest CreateRequest(
        uint correlationId)
    {
        return new DiscoverRequest(
            new CorrelationId(
                correlationId));
    }

    private static RuntimeEndpoint CreateRuntimeEndpoint()
    {
        var descriptor =
            new EndpointDescriptor(
                new EndpointId(
                    "coordinator-health-probe-endpoint"))
            {
                Metadata =
                    new EndpointMetadata
                    {
                        DisplayName =
                            "Coordinator Health Probe Endpoint",
                        Description =
                            "Endpoint used to test protocol health-probe "
                            + "timeouts."
                    }
            };

        var context =
            new RuntimeContext();

        return context.AddEndpoint(
            descriptor);
    }

    private sealed class TestProtocolSynchronizer
        : IRuntimeEndpointSynchronizer,
          IRuntimeProtocolEndpointSynchronizer
    {
        public Task SynchronizeAsync(
            ITransportConnection connection,
            RuntimeEndpoint runtimeEndpoint,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "The transport synchronization contract should not "
                + "be selected.");
        }

        public Task SynchronizeAsync(
            IRuntimeProtocolConnection connection,
            RuntimeEndpoint runtimeEndpoint,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                connection);

            ArgumentNullException.ThrowIfNull(
                runtimeEndpoint);

            cancellationToken.ThrowIfCancellationRequested();

            return Task.CompletedTask;
        }
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

        public Task<ITransportConnection> ConnectAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                _connection);
        }
    }

    private sealed class NonRespondingDuplexTransportConnection
        : ITransportDuplexConnection,
          ITransportConnectionInvalidator
    {
        private readonly TaskCompletionSource<byte[]>
            _receiveCompletion =
                new(
                    TaskCreationOptions.RunContinuationsAsynchronously);

        private TransportConnectionState _state =
            TransportConnectionState.Connected;

        public event EventHandler<
            TransportConnectionStateChangedEventArgs>?
            StateChanged;

        public TransportConnectionState State =>
            _state;

        public int SendCount
        {
            get;
            private set;
        }

        public Task<byte[]> ExchangeAsync(
            byte[] request,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "ExchangeAsync must not be used by a duplex health probe.");
        }

        public Task SendAsync(
            byte[] payload,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                payload);

            cancellationToken.ThrowIfCancellationRequested();

            if (_state != TransportConnectionState.Connected)
            {
                throw new InvalidOperationException(
                    "The test transport connection is not connected.");
            }

            SendCount++;

            return Task.CompletedTask;
        }

        public Task<byte[]> ReceiveAsync(
            CancellationToken cancellationToken = default)
        {
            return _receiveCompletion.Task.WaitAsync(
                cancellationToken);
        }

        public void Invalidate()
        {
            TransitionTo(
                TransportConnectionState.Faulted);
        }

        private void TransitionTo(
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