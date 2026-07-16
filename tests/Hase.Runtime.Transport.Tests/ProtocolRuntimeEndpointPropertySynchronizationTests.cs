using Hase.Core.Domain.Data;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Protocol;
using Hase.Runtime.Runtime;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class ProtocolRuntimeEndpointPropertySynchronizationTests
{
    [Fact]
    public async Task SynchronizeAsync_ShouldReadReadablePropertiesAndSkipWriteOnlyProperty()
    {
        // Arrange
        EndpointDescriptor descriptor =
            CreateDescriptor();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                descriptor);

        PropertyValue temperatureValue =
            CreatePropertyValue(
                21.5);

        PropertyValue setPointValue =
            CreatePropertyValue(
                22.0);

        var connection =
            new TestProtocolConnection(
                descriptor);

        connection.SetPropertyValue(
            TemperaturePropertyId,
            temperatureValue);

        connection.SetPropertyValue(
            SetPointPropertyId,
            setPointValue);

        var synchronizer =
            CreateSynchronizer();

        // Act
        await synchronizer.SynchronizeAsync(
            connection,
            runtimeEndpoint);

        // Assert
        Assert.Equal(
            3,
            connection.ExchangeCallCount);

        Assert.Equal(
            new[]
            {
                TemperaturePropertyId,
                SetPointPropertyId
            },
            connection.ReadPropertyRequests
                .Select(
                    request =>
                        request.PropertyId)
                .ToArray());

        Assert.DoesNotContain(
            connection.ReadPropertyRequests,
            request =>
                request.PropertyId
                == CalibrationPropertyId);
    }

    [Fact]
    public async Task SynchronizeAsync_ShouldPopulateRuntimePropertyCaches()
    {
        // Arrange
        EndpointDescriptor descriptor =
            CreateDescriptor();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                descriptor);

        PropertyValue temperatureValue =
            CreatePropertyValue(
                23.75);

        PropertyValue setPointValue =
            CreatePropertyValue(
                24.0);

        var connection =
            new TestProtocolConnection(
                descriptor);

        connection.SetPropertyValue(
            TemperaturePropertyId,
            temperatureValue);

        connection.SetPropertyValue(
            SetPointPropertyId,
            setPointValue);

        var synchronizer =
            CreateSynchronizer();

        // Act
        await synchronizer.SynchronizeAsync(
            connection,
            runtimeEndpoint);

        // Assert
        RuntimeInstrument runtimeInstrument =
            Assert.Single(
                runtimeEndpoint.Instruments);

        RuntimeProperty temperatureProperty =
            runtimeInstrument.FindProperty(
                TemperaturePropertyId)
            ?? throw new InvalidOperationException(
                "The temperature runtime property was not found.");

        RuntimeProperty setPointProperty =
            runtimeInstrument.FindProperty(
                SetPointPropertyId)
            ?? throw new InvalidOperationException(
                "The set-point runtime property was not found.");

        RuntimeProperty calibrationProperty =
            runtimeInstrument.FindProperty(
                CalibrationPropertyId)
            ?? throw new InvalidOperationException(
                "The calibration runtime property was not found.");

        AssertPropertyValueEquivalent(
            temperatureValue,
            temperatureProperty.CurrentValue);

        AssertPropertyValueEquivalent(
            setPointValue,
            setPointProperty.CurrentValue);

        Assert.Null(
            calibrationProperty.CurrentValue);
    }

    [Fact]
    public async Task SynchronizeAsync_ShouldSendCorrectInstrumentAndPropertyIdentifiers()
    {
        // Arrange
        EndpointDescriptor descriptor =
            CreateDescriptor();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                descriptor);

        var connection =
            new TestProtocolConnection(
                descriptor);

        connection.SetPropertyValue(
            TemperaturePropertyId,
            CreatePropertyValue(
                19.0));

        connection.SetPropertyValue(
            SetPointPropertyId,
            CreatePropertyValue(
                20.0));

        var synchronizer =
            CreateSynchronizer();

        // Act
        await synchronizer.SynchronizeAsync(
            connection,
            runtimeEndpoint);

        // Assert
        Assert.Equal(
            2,
            connection.ReadPropertyRequests.Count);

        Assert.All(
            connection.ReadPropertyRequests,
            request =>
                Assert.Equal(
                    InstrumentId,
                    request.InstrumentId));

        Assert.Equal(
            TemperaturePropertyId,
            connection.ReadPropertyRequests[0]
                .PropertyId);

        Assert.Equal(
            SetPointPropertyId,
            connection.ReadPropertyRequests[1]
                .PropertyId);

        Assert.All(
            connection.ReadPropertyRequests,
            request =>
                Assert.False(
                    request.CorrelationId.IsNone));

        Assert.NotEqual(
            connection.ReadPropertyRequests[0]
                .CorrelationId,
            connection.ReadPropertyRequests[1]
                .CorrelationId);
    }

    [Fact]
    public async Task SynchronizeAsync_ShouldNotifyRuntimePropertyObserver()
    {
        // Arrange
        EndpointDescriptor descriptor =
            CreateDescriptor();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                descriptor);

        RuntimeInstrument runtimeInstrument =
            Assert.Single(
                runtimeEndpoint.Instruments);

        RuntimeProperty temperatureProperty =
            runtimeInstrument.FindProperty(
                TemperaturePropertyId)
            ?? throw new InvalidOperationException(
                "The temperature runtime property was not found.");

        var observer =
            new TestPropertyValueObserver();

        temperatureProperty.Subscribe(
            observer);

        PropertyValue temperatureValue =
            CreatePropertyValue(
                18.25);

        var connection =
            new TestProtocolConnection(
                descriptor);

        connection.SetPropertyValue(
            TemperaturePropertyId,
            temperatureValue);

        connection.SetPropertyValue(
            SetPointPropertyId,
            CreatePropertyValue(
                19.0));

        var synchronizer =
            CreateSynchronizer();

        // Act
        await synchronizer.SynchronizeAsync(
            connection,
            runtimeEndpoint);

        // Assert
        PropertyValueChanged change =
            Assert.Single(
                observer.Changes);

        Assert.Same(
            temperatureProperty,
            change.Property);

        Assert.Null(
            change.PreviousValue);

        AssertPropertyValueEquivalent(
            temperatureValue,
            change.CurrentValue);

        temperatureProperty.Unsubscribe(
            observer);
    }

    private static readonly EndpointId EndpointId =
        new(
            "property-sync-endpoint");

    private static readonly InstrumentId InstrumentId =
        new(
            "environment-controller");

    private static readonly PropertyId TemperaturePropertyId =
        new(
            "environment.temperature");

    private static readonly PropertyId SetPointPropertyId =
        new(
            "environment.set-point");

    private static readonly PropertyId CalibrationPropertyId =
        new(
            "environment.calibration");

    private static ProtocolRuntimeEndpointSynchronizer
        CreateSynchronizer()
    {
        return new ProtocolRuntimeEndpointSynchronizer(
            new EndpointDescriptorCompatibilityValidator());
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
        PropertyDescriptor temperatureProperty =
            CreatePropertyDescriptor(
                TemperaturePropertyId,
                "Temperature",
                PropertyAccessMode.Read);

        PropertyDescriptor setPointProperty =
            CreatePropertyDescriptor(
                SetPointPropertyId,
                "Set Point",
                PropertyAccessMode.ReadWrite);

        PropertyDescriptor calibrationProperty =
            CreatePropertyDescriptor(
                CalibrationPropertyId,
                "Calibration",
                PropertyAccessMode.Write);

        var instrument =
            new InstrumentDescriptor(
                InstrumentId,
                "Environment Controller",
                new InstrumentKind(
                    "environment-controller"))
            {
                Interface =
                    new InstrumentInterface(
                        properties:
                        [
                            temperatureProperty,
                            setPointProperty,
                            calibrationProperty
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
                        "Property Synchronization Endpoint",
                    Description =
                        "Endpoint used to test initial property "
                        + "synchronization."
                }
        };
    }

    private static PropertyDescriptor CreatePropertyDescriptor(
        PropertyId propertyId,
        string displayName,
        PropertyAccessMode accessMode)
    {
        return new PropertyDescriptor(
            propertyId,
            new DescriptorPath(
                "Environment",
                displayName.Replace(
                    " ",
                    string.Empty)),
            displayName,
            new NumericDataDescriptor(
                Quantities.Temperature,
                Units.Celsius))
        {
            AccessMode =
                accessMode
        };
    }

    private static PropertyValue CreatePropertyValue(
        double value)
    {
        return new PropertyValue(
            value,
            DateTimeOffset.UtcNow);
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

        DateTimeOffset expectedProtocolTimestamp =
            DateTimeOffset.FromUnixTimeMilliseconds(
                expected.TimestampUtc.ToUnixTimeMilliseconds());

        Assert.Equal(
            expectedProtocolTimestamp,
            actual.TimestampUtc);

        Assert.Equal(
            expected.Quality,
            actual.Quality);
    }

    private sealed class TestPropertyValueObserver
        : IPropertyValueObserver
    {
        private readonly List<PropertyValueChanged> _changes =
            new();

        public IReadOnlyList<PropertyValueChanged> Changes =>
            _changes;

        public void OnPropertyValueChanged(
            PropertyValueChanged change)
        {
            ArgumentNullException.ThrowIfNull(
                change);

            _changes.Add(
                change);
        }
    }

    private sealed class TestProtocolConnection
        : ITransportConnection
    {
        private readonly EndpointDescriptor _descriptor;

        private readonly Dictionary<PropertyId, PropertyValue>
            _propertyValues =
                new();

        private readonly List<ReadPropertyRequest>
            _readPropertyRequests =
                new();

        private readonly BinaryProtocolPayloadCodec _payloadCodec =
            new();

        private readonly ProtocolEnvelopeByteCodec _envelopeByteCodec =
            new();

        public TestProtocolConnection(
            EndpointDescriptor descriptor)
        {
            _descriptor =
                descriptor
                ?? throw new ArgumentNullException(
                    nameof(descriptor));
        }

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

        public int ExchangeCallCount
        {
            get;
            private set;
        }

        public IReadOnlyList<ReadPropertyRequest>
            ReadPropertyRequests =>
                _readPropertyRequests;

        public void SetPropertyValue(
            PropertyId propertyId,
            PropertyValue propertyValue)
        {
            ArgumentNullException.ThrowIfNull(
                propertyId);

            ArgumentNullException.ThrowIfNull(
                propertyValue);

            _propertyValues[propertyId] =
                propertyValue;
        }

        public Task<byte[]> ExchangeAsync(
            byte[] request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                request);

            cancellationToken.ThrowIfCancellationRequested();

            ExchangeCallCount++;

            ProtocolEnvelope requestEnvelope =
                _envelopeByteCodec.Decode(
                    request);

            ProtocolMessage requestMessage =
                _payloadCodec.Decode(
                    requestEnvelope);

            ProtocolMessage responseMessage =
                requestMessage switch
                {
                    ReadEndpointDescriptorRequest descriptorRequest =>
                        CreateDescriptorResponse(
                            descriptorRequest),

                    ReadPropertyRequest propertyRequest =>
                        CreatePropertyResponse(
                            propertyRequest),

                    _ =>
                        throw new InvalidDataException(
                            $"Unsupported test request type "
                            + $"'{requestMessage.MessageType}'.")
                };

            ProtocolEnvelope responseEnvelope =
                _payloadCodec.Encode(
                    responseMessage);

            byte[] responseFrame =
                _envelopeByteCodec.Encode(
                    responseEnvelope);

            return Task.FromResult(
                responseFrame);
        }

        private ReadEndpointDescriptorResponse
            CreateDescriptorResponse(
                ReadEndpointDescriptorRequest request)
        {
            return new ReadEndpointDescriptorResponse(
                request.CorrelationId,
                ProtocolResult.Success,
                _descriptor);
        }

        private ReadPropertyResponse CreatePropertyResponse(
            ReadPropertyRequest request)
        {
            _readPropertyRequests.Add(
                request);

            if (!_propertyValues.TryGetValue(
                    request.PropertyId,
                    out PropertyValue? propertyValue))
            {
                return new ReadPropertyResponse(
                    request.CorrelationId,
                    ProtocolResult.NotFound,
                    null);
            }

            return new ReadPropertyResponse(
                request.CorrelationId,
                ProtocolResult.Success,
                propertyValue);
        }
    }
}