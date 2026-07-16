using Hase.Core.Domain.Data;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Protocol;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport;
using Hase.Transport.Loopback;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class RuntimeEndpointConnectionCoordinatorFailureIntegrationTests
{
    [Fact]
    public async Task ConnectAsync_WhenPropertySynchronizationFails_ShouldBecomeFaulted()
    {
        // Arrange
        EndpointDescriptor descriptor =
            CreateDescriptor();

        int descriptorRequestCount =
            0;

        int propertyRequestCount =
            0;

        var connection =
            new LoopbackTransportConnection(
                (
                    requestFrame,
                    cancellationToken) =>
                {
                    return HandleProtocolExchangeAsync(
                        requestFrame,
                        () =>
                        {
                            descriptorRequestCount++;
                        },
                        () =>
                        {
                            propertyRequestCount++;
                        },
                        cancellationToken);
                });

        var factory =
            new TestTransportFactory(
                connection);

        await using var connectionManager =
            new TransportConnectionManager(
                factory);

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                CreateDescriptor());

        RuntimeProperty runtimeProperty =
            GetRuntimeProperty(
                runtimeEndpoint);

        var statusObserver =
            new TestConnectionStatusObserver();

        runtimeEndpoint.SubscribeConnectionStatus(
            statusObserver);

        var synchronizer =
            new ProtocolRuntimeEndpointSynchronizer(
                new EndpointDescriptorCompatibilityValidator());

        await using var coordinator =
            new RuntimeEndpointConnectionCoordinator(
                connectionManager,
                runtimeEndpoint,
                synchronizer);

        Assert.Equal(
            EndpointConnectionState.Disconnected,
            runtimeEndpoint.ConnectionStatus.State);

        Assert.Null(
            runtimeProperty.CurrentValue);

        // Act
        Task Act()
        {
            return coordinator.ConnectAsync();
        }

        // Assert
        InvalidDataException exception =
            await Assert.ThrowsAsync<InvalidDataException>(
                Act);

        Assert.Equal(
            "The endpoint returned result 'Rejected' while reading "
            + "property 'environment.temperature': "
            + "Property access was rejected.",
            exception.Message);

        Assert.Equal(
            EndpointConnectionState.Faulted,
            runtimeEndpoint.ConnectionStatus.State);

        Assert.NotNull(
            runtimeEndpoint.ConnectionStatus.ChangedAtUtc);

        Assert.Equal(
            TimeSpan.Zero,
            runtimeEndpoint.ConnectionStatus
                .ChangedAtUtc
                .Value
                .Offset);

        Assert.Equal(
            "Endpoint synchronization failed.",
            runtimeEndpoint.ConnectionStatus.Detail);

        Assert.DoesNotContain(
            EndpointConnectionState.Ready,
            statusObserver.States);

        Assert.Equal(
            new[]
            {
                EndpointConnectionState.Disconnected,
                EndpointConnectionState.Connecting,
                EndpointConnectionState.Synchronizing,
                EndpointConnectionState.Faulted
            },
            CollapseAdjacentDuplicates(
                statusObserver.States));

        Assert.Equal(
            1,
            factory.ConnectCallCount);

        Assert.Equal(
            1,
            descriptorRequestCount);

        Assert.Equal(
            1,
            propertyRequestCount);

        Assert.Same(
            connection,
            connectionManager.CurrentConnection);

        Assert.Equal(
            TransportConnectionState.Connected,
            connectionManager.CurrentState);

        Assert.Null(
            runtimeProperty.CurrentValue);

        runtimeEndpoint.UnsubscribeConnectionStatus(
            statusObserver);
    }

    private static Task<byte[]> HandleProtocolExchangeAsync(
        byte[] requestFrame,
        Action descriptorRequestReceived,
        Action propertyRequestReceived,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            requestFrame);

        ArgumentNullException.ThrowIfNull(
            descriptorRequestReceived);

        ArgumentNullException.ThrowIfNull(
            propertyRequestReceived);

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
                    CreateDescriptorResponse(
                        request,
                        descriptorRequestReceived),

                ReadPropertyRequest request =>
                    CreateRejectedPropertyResponse(
                        request,
                        propertyRequestReceived),

                _ =>
                    throw new InvalidDataException(
                        $"The integration endpoint does not support "
                        + $"request type "
                        + $"'{requestMessage.MessageType}'.")
            };

        ProtocolEnvelope responseEnvelope =
            payloadCodec.Encode(
                responseMessage);

        byte[] responseFrame =
            envelopeByteCodec.Encode(
                responseEnvelope);

        return Task.FromResult(
            responseFrame);
    }

    private static ReadEndpointDescriptorResponse
        CreateDescriptorResponse(
            ReadEndpointDescriptorRequest request,
            Action descriptorRequestReceived)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        ArgumentNullException.ThrowIfNull(
            descriptorRequestReceived);

        descriptorRequestReceived();

        if (request.EndpointId
            != EndpointId)
        {
            throw new InvalidDataException(
                $"Expected endpoint identifier "
                + $"'{EndpointId.Value}', but received "
                + $"'{request.EndpointId.Value}'.");
        }

        if (request.CorrelationId.IsNone)
        {
            throw new InvalidDataException(
                "The descriptor request must contain a correlation "
                + "identifier.");
        }

        return new ReadEndpointDescriptorResponse(
            request.CorrelationId,
            ProtocolResult.Success,
            CreateDescriptor());
    }

    private static ReadPropertyResponse
        CreateRejectedPropertyResponse(
            ReadPropertyRequest request,
            Action propertyRequestReceived)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        ArgumentNullException.ThrowIfNull(
            propertyRequestReceived);

        propertyRequestReceived();

        if (request.InstrumentId
            != InstrumentId)
        {
            throw new InvalidDataException(
                $"Expected instrument identifier "
                + $"'{InstrumentId.Value}', but received "
                + $"'{request.InstrumentId.Value}'.");
        }

        if (request.PropertyId
            != TemperaturePropertyId)
        {
            throw new InvalidDataException(
                $"Expected property identifier "
                + $"'{TemperaturePropertyId.Value}', but received "
                + $"'{request.PropertyId.Value}'.");
        }

        if (request.CorrelationId.IsNone)
        {
            throw new InvalidDataException(
                "The property request must contain a correlation "
                + "identifier.");
        }

        return new ReadPropertyResponse(
            request.CorrelationId,
            new ProtocolResult(
                ProtocolResultCode.Rejected,
                "Property access was rejected"),
            null);
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

    private static RuntimeProperty GetRuntimeProperty(
        RuntimeEndpoint runtimeEndpoint)
    {
        ArgumentNullException.ThrowIfNull(
            runtimeEndpoint);

        RuntimeInstrument runtimeInstrument =
            Assert.Single(
                runtimeEndpoint.Instruments);

        return runtimeInstrument.FindProperty(
                   TemperaturePropertyId)
               ?? throw new InvalidOperationException(
                   $"Runtime property "
                   + $"'{TemperaturePropertyId.Value}' was not found.");
    }

    private static EndpointDescriptor CreateDescriptor()
    {
        var temperatureProperty =
            new PropertyDescriptor(
                TemperaturePropertyId,
                new DescriptorPath(
                    "Environment",
                    "Temperature"),
                "Temperature",
                new NumericDataDescriptor(
                    Quantities.Temperature,
                    Units.Celsius))
            {
                AccessMode =
                    PropertyAccessMode.Read,
                Description =
                    "Current measured temperature."
            };

        var instrument =
            new InstrumentDescriptor(
                InstrumentId,
                "Environment Sensor",
                new InstrumentKind(
                    "environment-sensor"))
            {
                Interface =
                    new InstrumentInterface(
                        properties:
                        [
                            temperatureProperty
                        ])
            };

        return new EndpointDescriptor(
            EndpointId,
            [
                instrument
            ])
        {
            Metadata =
                new EndpointMetadata
                {
                    DisplayName =
                        "Runtime Transport Failure Integration Endpoint",
                    Description =
                        "Endpoint used to verify synchronization failure "
                        + "through the complete runtime and protocol pipeline."
                }
        };
    }

    private static readonly EndpointId EndpointId =
        new(
            "integration-failure-endpoint-01");

    private static readonly InstrumentId InstrumentId =
        new(
            "environment-sensor-01");

    private static readonly PropertyId TemperaturePropertyId =
        new(
            "environment.temperature");

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