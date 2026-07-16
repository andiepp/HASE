using Hase.Core.Domain.Data;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
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

        PropertyValue physicalPropertyValue =
            CreatePhysicalPropertyValue();

        int descriptorRequestCount =
            0;

        int propertyRequestCount =
            0;

        var connection =
            new LoopbackTransportConnection(
                async (
                    requestFrame,
                    cancellationToken) =>
                {
                    return await HandleProtocolExchangeAsync(
                        dispatcher,
                        requestFrame,
                        physicalPropertyValue,
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

        RuntimeEndpoint clientEndpoint =
            CreateRuntimeEndpoint(
                CreateDescriptor());

        RuntimeProperty clientProperty =
            GetRuntimeProperty(
                clientEndpoint);

        Assert.Null(
            clientProperty.CurrentValue);

        var statusObserver =
            new TestConnectionStatusObserver(
                clientEndpoint,
                clientProperty);

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
            1,
            descriptorRequestCount);

        Assert.Equal(
            1,
            propertyRequestCount);

        AssertPropertyValueEquivalent(
            physicalPropertyValue,
            clientProperty.CurrentValue);

        Assert.True(
            statusObserver.PropertyWasPopulatedWhenReady);

        AssertPropertyValueEquivalent(
            physicalPropertyValue,
            statusObserver.PropertyValueObservedWhenReady);

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
        PropertyValue physicalPropertyValue,
        Action descriptorRequestReceived,
        Action propertyRequestReceived,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            dispatcher);

        ArgumentNullException.ThrowIfNull(
            requestFrame);

        ArgumentNullException.ThrowIfNull(
            physicalPropertyValue);

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

        ProtocolMessage responseMessage;

        switch (requestMessage)
        {
            case ReadEndpointDescriptorRequest request:
                descriptorRequestReceived();

                responseMessage =
                    await dispatcher.DispatchAsync(
                        request,
                        cancellationToken);
                break;

            case ReadPropertyRequest request:
                propertyRequestReceived();

                ValidatePropertyRequest(
                    request);

                responseMessage =
                    new ReadPropertyResponse(
                        request.CorrelationId,
                        ProtocolResult.Success,
                        physicalPropertyValue);
                break;

            default:
                throw new InvalidDataException(
                    $"The integration endpoint does not support "
                    + $"request type "
                    + $"'{requestMessage.MessageType}'.");
        }

        ProtocolEnvelope responseEnvelope =
            payloadCodec.Encode(
                responseMessage);

        return envelopeByteCodec.Encode(
            responseEnvelope);
    }

    private static void ValidatePropertyRequest(
        ReadPropertyRequest request)
    {
        ArgumentNullException.ThrowIfNull(
            request);

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
                        "Runtime Transport Integration Endpoint",
                    Description =
                        "Endpoint used to verify the complete runtime, "
                        + "transport, and protocol synchronization pipeline."
                }
        };
    }

    private static PropertyValue CreatePhysicalPropertyValue()
    {
        return new PropertyValue(
            22.75,
            DateTimeOffset.FromUnixTimeMilliseconds(
                1_750_000_000_123),
            PropertyQuality.Good);
    }

    private static RuntimeProperty GetRuntimeProperty(
        RuntimeEndpoint runtimeEndpoint)
    {
        RuntimeInstrument runtimeInstrument =
            Assert.Single(
                runtimeEndpoint.Instruments);

        return runtimeInstrument.FindProperty(
                   TemperaturePropertyId)
               ?? throw new InvalidOperationException(
                   $"Runtime property "
                   + $"'{TemperaturePropertyId.Value}' was not found.");
    }

    private static void AssertPropertyValueEquivalent(
        PropertyValue expected,
        PropertyValue? actual)
    {
        ArgumentNullException.ThrowIfNull(
            expected);

        Assert.NotNull(
            actual);

        Assert.Equal(
            expected.Value,
            actual!.Value);

        Assert.Equal(
            expected.TimestampUtc,
            actual.TimestampUtc);

        Assert.Equal(
            expected.Quality,
            actual.Quality);
    }

    private static readonly EndpointId EndpointId =
        new(
            "integration-endpoint-01");

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
        private readonly RuntimeEndpoint _runtimeEndpoint;
        private readonly RuntimeProperty _runtimeProperty;

        private readonly List<EndpointConnectionState> _states =
            new();

        public TestConnectionStatusObserver(
            RuntimeEndpoint runtimeEndpoint,
            RuntimeProperty runtimeProperty)
        {
            _runtimeEndpoint =
                runtimeEndpoint
                ?? throw new ArgumentNullException(
                    nameof(runtimeEndpoint));

            _runtimeProperty =
                runtimeProperty
                ?? throw new ArgumentNullException(
                    nameof(runtimeProperty));
        }

        public IReadOnlyList<EndpointConnectionState> States =>
            _states;

        public bool PropertyWasPopulatedWhenReady
        {
            get;
            private set;
        }

        public PropertyValue? PropertyValueObservedWhenReady
        {
            get;
            private set;
        }

        public void OnEndpointConnectionStatusChanged(
            EndpointConnectionStatusChanged change)
        {
            ArgumentNullException.ThrowIfNull(
                change);

            Assert.Same(
                _runtimeEndpoint,
                change.Endpoint);

            _states.Add(
                change.CurrentStatus.State);

            if (change.CurrentStatus.State
                != EndpointConnectionState.Ready)
            {
                return;
            }

            PropertyValueObservedWhenReady =
                _runtimeProperty.CurrentValue;

            PropertyWasPopulatedWhenReady =
                PropertyValueObservedWhenReady is not null;
        }
    }
}