using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Protocol;
using Hase.Runtime.Connections;
using Hase.Runtime.Protocol;
using Hase.Runtime.Runtime;
using Hase.Transport;
using Hase.Transport.Loopback;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class RuntimeEndpointConnectionCoordinatorIntegrationTests
{
    [Fact]
    public async Task ConnectAsync_WithRealProtocolSynchronization_ShouldBecomeReady()
    {
        // Arrange
        EndpointDescriptor descriptor =
            CreateDescriptor();

        RuntimeEndpoint physicalEndpoint =
            CreateRuntimeEndpoint(
                descriptor);

        var dispatcher =
            new RuntimeProtocolDispatcher(
                physicalEndpoint);

        var connection =
            new LoopbackTransportConnection(
                async (
                    requestFrame,
                    cancellationToken) =>
                {
                    return await HandleProtocolExchangeAsync(
                        dispatcher,
                        requestFrame,
                        cancellationToken);
                });

        var factory =
            new TestTransportFactory(
                connection);

        await using var connectionManager =
            new TransportConnectionManager(
                factory);

        RuntimeEndpoint clientEndpoint =
            CreateRuntimeEndpoint(
                CreateDescriptor());

        var statusObserver =
            new TestConnectionStatusObserver();

        clientEndpoint.SubscribeConnectionStatus(
            statusObserver);

        var synchronizer =
            new ProtocolRuntimeEndpointSynchronizer(
                new EndpointDescriptorCompatibilityValidator());

        await using var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                connectionManager,
                clientEndpoint,
                synchronizer);

        Assert.Equal(
            EndpointConnectionState.Disconnected,
            clientEndpoint.ConnectionStatus.State);

        // Act
        ITransportConnection actualConnection =
            await coordinator.ConnectAsync();

        // Assert
        Assert.Same(
            connection,
            actualConnection);

        Assert.Same(
            connection,
            connectionManager.CurrentConnection);

        Assert.Equal(
            TransportConnectionState.Connected,
            connectionManager.CurrentState);

        Assert.Equal(
            EndpointConnectionState.Ready,
            clientEndpoint.ConnectionStatus.State);

        Assert.NotNull(
            clientEndpoint.ConnectionStatus.ChangedAtUtc);

        Assert.Equal(
            TimeSpan.Zero,
            clientEndpoint.ConnectionStatus
                .ChangedAtUtc
                .Value
                .Offset);

        Assert.Equal(
            "The endpoint is connected, synchronized, and ready.",
            clientEndpoint.ConnectionStatus.Detail);

        Assert.Equal(
            1,
            factory.ConnectCallCount);

        Assert.Equal(
            new[]
            {
                EndpointConnectionState.Disconnected,
                EndpointConnectionState.Connecting,
                EndpointConnectionState.Synchronizing,
                EndpointConnectionState.Ready
            },
            CollapseAdjacentDuplicates(
                statusObserver.States));

        clientEndpoint.UnsubscribeConnectionStatus(
            statusObserver);
    }

    private static async Task<byte[]> HandleProtocolExchangeAsync(
        IRuntimeProtocolDispatcher dispatcher,
        byte[] requestFrame,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            dispatcher);

        ArgumentNullException.ThrowIfNull(
            requestFrame);

        cancellationToken.ThrowIfCancellationRequested();

        var envelopeByteCodec =
            new ProtocolEnvelopeByteCodec();

        ProtocolEnvelope requestEnvelope =
            envelopeByteCodec.Decode(
                requestFrame);

        var payloadCodec =
            new BinaryProtocolPayloadCodec();

        ProtocolMessage requestMessage =
            payloadCodec.Decode(
                requestEnvelope);

        ProtocolMessage responseMessage =
            requestMessage switch
            {
                ReadEndpointDescriptorRequest request =>
                    await dispatcher.DispatchAsync(
                        request,
                        cancellationToken),

                _ =>
                    throw new InvalidDataException(
                        $"The integration endpoint does not support "
                        + $"request type "
                        + $"'{requestMessage.MessageType}'.")
            };

        ProtocolEnvelope responseEnvelope =
            payloadCodec.Encode(
                responseMessage);

        return envelopeByteCodec.Encode(
            responseEnvelope);
    }

    private static IReadOnlyList<EndpointConnectionState>
        CollapseAdjacentDuplicates(
            IReadOnlyList<EndpointConnectionState> states)
    {
        ArgumentNullException.ThrowIfNull(
            states);

        var result =
            new List<EndpointConnectionState>();

        foreach (EndpointConnectionState state
                 in states)
        {
            if (result.Count == 0
                || result[^1] != state)
            {
                result.Add(
                    state);
            }
        }

        return result;
    }

    private static RuntimeEndpoint CreateRuntimeEndpoint(
        EndpointDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(
            descriptor);

        var context =
            new RuntimeContext();

        return context.AddEndpoint(
            descriptor);
    }

    private static EndpointDescriptor CreateDescriptor()
    {
        return new EndpointDescriptor(
            new EndpointId(
                "integration-endpoint-01"))
        {
            Metadata =
                new EndpointMetadata
                {
                    DisplayName =
                        "Runtime Transport Integration Endpoint",
                    Description =
                        "Endpoint used to verify the complete runtime, "
                        + "transport, and protocol synchronization pipeline."
                }
        };
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

    private sealed class TestConnectionStatusObserver
        : IEndpointConnectionStatusObserver
    {
        private readonly List<EndpointConnectionState> _states =
            new();

        public IReadOnlyList<EndpointConnectionState> States =>
            _states;

        public void OnEndpointConnectionStatusChanged(
            EndpointConnectionStatusChanged change)
        {
            ArgumentNullException.ThrowIfNull(
                change);

            _states.Add(
                change.CurrentStatus.State);
        }
    }
}