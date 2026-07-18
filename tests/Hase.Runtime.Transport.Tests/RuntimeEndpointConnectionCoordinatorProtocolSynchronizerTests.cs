using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class
    RuntimeEndpointConnectionCoordinatorProtocolSynchronizerTests
{
    [Fact]
    public async Task ConnectAsync_ProtocolSynchronizer_ShouldUseProtocolConnection()
    {
        // Arrange
        var transportConnection =
            new TestTransportConnection();

        var transportFactory =
            new TestTransportFactory(
                transportConnection);

        await using var connectionManager =
            new TransportConnectionManager(
                transportFactory);

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint();

        var synchronizer =
            new DualContractSynchronizer();

        await using var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                connectionManager,
                runtimeEndpoint,
                synchronizer);

        using var cancellationSource =
            new CancellationTokenSource();

        // Act
        ITransportConnection result =
            await coordinator.ConnectAsync(
                cancellationSource.Token);

        // Assert
        Assert.Same(
            transportConnection,
            result);

        Assert.Equal(
            0,
            synchronizer.TransportSynchronizationCount);

        Assert.Equal(
            1,
            synchronizer.ProtocolSynchronizationCount);

        Assert.IsType<LegacyRuntimeProtocolConnection>(
            synchronizer.ReceivedProtocolConnection);

        Assert.Same(
            runtimeEndpoint,
            synchronizer.ReceivedRuntimeEndpoint);

        Assert.Equal(
            cancellationSource.Token,
            synchronizer.ReceivedCancellationToken);

        Assert.Equal(
            EndpointConnectionState.Ready,
            runtimeEndpoint.ConnectionStatus.State);

        Assert.Equal(
            0,
            transportConnection.ExchangeCount);
    }

    private static RuntimeEndpoint CreateRuntimeEndpoint()
    {
        var descriptor =
            new EndpointDescriptor(
                new EndpointId(
                    "coordinator-protocol-endpoint"))
            {
                Metadata =
                    new EndpointMetadata
                    {
                        DisplayName =
                            "Coordinator Protocol Endpoint",
                        Description =
                            "Endpoint used to test protocol synchronizer "
                            + "selection."
                    }
            };

        var context =
            new RuntimeContext();

        return context.AddEndpoint(
            descriptor);
    }

    private sealed class DualContractSynchronizer
        : IRuntimeEndpointSynchronizer,
          IRuntimeProtocolEndpointSynchronizer
    {
        public int TransportSynchronizationCount
        {
            get;
            private set;
        }

        public int ProtocolSynchronizationCount
        {
            get;
            private set;
        }

        public IRuntimeProtocolConnection?
            ReceivedProtocolConnection
        {
            get;
            private set;
        }

        public RuntimeEndpoint? ReceivedRuntimeEndpoint
        {
            get;
            private set;
        }

        public CancellationToken ReceivedCancellationToken
        {
            get;
            private set;
        }

        public Task SynchronizeAsync(
            ITransportConnection connection,
            RuntimeEndpoint runtimeEndpoint,
            CancellationToken cancellationToken = default)
        {
            TransportSynchronizationCount++;

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

            ProtocolSynchronizationCount++;

            ReceivedProtocolConnection =
                connection;

            ReceivedRuntimeEndpoint =
                runtimeEndpoint;

            ReceivedCancellationToken =
                cancellationToken;

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

    private sealed class TestTransportConnection
        : ITransportConnection
    {
        public event EventHandler<
            TransportConnectionStateChangedEventArgs>?
            StateChanged
        {
            add
            {
            }

            remove
            {
            }
        }

        public TransportConnectionState State =>
            TransportConnectionState.Connected;

        public int ExchangeCount
        {
            get;
            private set;
        }

        public Task<byte[]> ExchangeAsync(
            byte[] request,
            CancellationToken cancellationToken = default)
        {
            ExchangeCount++;

            throw new InvalidOperationException(
                "A transport exchange should not be required by this test.");
        }
    }
}