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

public sealed class ProtocolRuntimeEndpointPropertySynchronizationFailureTests
{
    [Fact]
    public async Task SynchronizeAsync_UnsuccessfulPropertyResult_ShouldThrow()
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

        connection.PropertyResponseFactory =
            request =>
                new ReadPropertyResponse(
                    request.CorrelationId,
                    new ProtocolResult(
                        ProtocolResultCode.Rejected,
                        "Property access was rejected"),
                    null);

        var synchronizer =
            CreateSynchronizer();

        // Act
        Task Act()
        {
            return synchronizer.SynchronizeAsync(
                connection,
                runtimeEndpoint);
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
            2,
            connection.ExchangeCallCount);

        Assert.Single(
            connection.ReadPropertyRequests);

        AssertAllPropertyCachesEmpty(
            runtimeEndpoint);
    }

    [Fact]
    public async Task SynchronizeAsync_SuccessfulResponseWithoutValue_ShouldThrow()
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

        connection.PropertyResponseFactory =
            request =>
                new ReadPropertyResponse(
                    request.CorrelationId,
                    ProtocolResult.Success,
                    null);

        var synchronizer =
            CreateSynchronizer();

        // Act
        Task Act()
        {
            return synchronizer.SynchronizeAsync(
                connection,
                runtimeEndpoint);
        }

        // Assert
        InvalidDataException exception =
            await Assert.ThrowsAsync<InvalidDataException>(
                Act);

        Assert.Equal(
            "The successful response for property "
            + "'environment.temperature' did not contain "
            + "a property value.",
            exception.Message);

        Assert.Equal(
            2,
            connection.ExchangeCallCount);

        Assert.Single(
            connection.ReadPropertyRequests);

        AssertAllPropertyCachesEmpty(
            runtimeEndpoint);
    }

    [Fact]
    public async Task SynchronizeAsync_WrongPropertyResponseType_ShouldThrow()
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

        connection.PropertyResponseFactory =
            request =>
                new DiscoverResponse(
                    request.CorrelationId,
                    EndpointId,
                    [
                        InstrumentId
                    ]);

        var synchronizer =
            CreateSynchronizer();

        // Act
        Task Act()
        {
            return synchronizer.SynchronizeAsync(
                connection,
                runtimeEndpoint);
        }

        // Assert
        InvalidDataException exception =
            await Assert.ThrowsAsync<InvalidDataException>(
                Act);

        Assert.Equal(
            "The response for property "
            + "'environment.temperature' did not decode as a "
            + "ReadPropertyResponse.",
            exception.Message);

        Assert.Equal(
            2,
            connection.ExchangeCallCount);

        Assert.Single(
            connection.ReadPropertyRequests);

        AssertAllPropertyCachesEmpty(
            runtimeEndpoint);
    }

    [Fact]
    public async Task SynchronizeAsync_PropertyCorrelationMismatch_ShouldThrow()
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

        connection.PropertyResponseFactory =
            request =>
                new ReadPropertyResponse(
                    CreateDifferentCorrelationId(
                        request.CorrelationId),
                    ProtocolResult.Success,
                    CreatePropertyValue(
                        20.0));

        var synchronizer =
            CreateSynchronizer();

        // Act
        Task Act()
        {
            return synchronizer.SynchronizeAsync(
                connection,
                runtimeEndpoint);
        }

        // Assert
        InvalidDataException exception =
            await Assert.ThrowsAsync<InvalidDataException>(
                Act);

        Assert.Equal(
            "The property 'environment.temperature' response "
            + "correlation identifier does not match the request.",
            exception.Message);

        Assert.Equal(
            2,
            connection.ExchangeCallCount);

        Assert.Single(
            connection.ReadPropertyRequests);

        AssertAllPropertyCachesEmpty(
            runtimeEndpoint);
    }

    [Fact]
    public async Task SynchronizeAsync_CancelledBetweenPropertyReads_ShouldThrow()
    {
        // Arrange
        EndpointDescriptor descriptor =
            CreateDescriptor();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                descriptor);

        PropertyValue firstValue =
            CreatePropertyValue(
                21.25);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        var connection =
            new TestProtocolConnection(
                descriptor);

        connection.PropertyResponseFactory =
            request =>
            {
                if (request.PropertyId
                    != TemperaturePropertyId)
                {
                    throw new InvalidOperationException(
                        "A second property request was not expected.");
                }

                cancellationTokenSource.Cancel();

                return new ReadPropertyResponse(
                    request.CorrelationId,
                    ProtocolResult.Success,
                    firstValue);
            };

        var synchronizer =
            CreateSynchronizer();

        // Act
        Task Act()
        {
            return synchronizer.SynchronizeAsync(
                connection,
                runtimeEndpoint,
                cancellationTokenSource.Token);
        }

        // Assert
        OperationCanceledException exception =
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                Act);

        Assert.Equal(
            cancellationTokenSource.Token,
            exception.CancellationToken);

        Assert.Equal(
            2,
            connection.ExchangeCallCount);

        ReadPropertyRequest request =
            Assert.Single(
                connection.ReadPropertyRequests);

        Assert.Equal(
            TemperaturePropertyId,
            request.PropertyId);

        RuntimeInstrument runtimeInstrument =
            Assert.Single(
                runtimeEndpoint.Instruments);

        RuntimeProperty temperatureProperty =
            GetRuntimeProperty(
                runtimeInstrument,
                TemperaturePropertyId);

        RuntimeProperty setPointProperty =
            GetRuntimeProperty(
                runtimeInstrument,
                SetPointPropertyId);

        AssertPropertyValueEquivalent(
            firstValue,
            temperatureProperty.CurrentValue);

        Assert.Null(
            setPointProperty.CurrentValue);
    }

    [Fact]
    public async Task SynchronizeAsync_LaterPropertyFailure_ShouldPreserveEarlierValue()
    {
        // Arrange
        EndpointDescriptor descriptor =
            CreateDescriptor();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                descriptor);

        PropertyValue firstValue =
            CreatePropertyValue(
                24.5);

        var connection =
            new TestProtocolConnection(
                descriptor);

        connection.PropertyResponseFactory =
            request =>
            {
                if (request.PropertyId
                    == TemperaturePropertyId)
                {
                    return new ReadPropertyResponse(
                        request.CorrelationId,
                        ProtocolResult.Success,
                        firstValue);
                }

                if (request.PropertyId
                    == SetPointPropertyId)
                {
                    return new ReadPropertyResponse(
                        request.CorrelationId,
                        ProtocolResult.NotFound,
                        null);
                }

                throw new InvalidOperationException(
                    $"Unexpected property identifier "
                    + $"'{request.PropertyId.Value}'.");
            };

        var synchronizer =
            CreateSynchronizer();

        // Act
        Task Act()
        {
            return synchronizer.SynchronizeAsync(
                connection,
                runtimeEndpoint);
        }

        // Assert
        InvalidDataException exception =
            await Assert.ThrowsAsync<InvalidDataException>(
                Act);

        Assert.Equal(
            "The endpoint returned result 'NotFound' while reading "
            + "property 'environment.set-point': (no message).",
            exception.Message);

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

        RuntimeInstrument runtimeInstrument =
            Assert.Single(
                runtimeEndpoint.Instruments);

        RuntimeProperty temperatureProperty =
            GetRuntimeProperty(
                runtimeInstrument,
                TemperaturePropertyId);

        RuntimeProperty setPointProperty =
            GetRuntimeProperty(
                runtimeInstrument,
                SetPointPropertyId);

        AssertPropertyValueEquivalent(
            firstValue,
            temperatureProperty.CurrentValue);

        Assert.Null(
            setPointProperty.CurrentValue);
    }

    private static readonly EndpointId EndpointId =
        new(
            "property-sync-failure-endpoint");

    private static readonly InstrumentId InstrumentId =
        new(
            "environment-controller");

    private static readonly PropertyId TemperaturePropertyId =
        new(
            "environment.temperature");

    private static readonly PropertyId SetPointPropertyId =
        new(
            "environment.set-point");

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
                "Temperature");

        PropertyDescriptor setPointProperty =
            CreatePropertyDescriptor(
                SetPointPropertyId,
                "Set Point");

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
                            setPointProperty
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
                        "Property Synchronization Failure Endpoint",
                    Description =
                        "Endpoint used to test property synchronization "
                        + "failure and cancellation behavior."
                }
        };
    }

    private static PropertyDescriptor CreatePropertyDescriptor(
        PropertyId propertyId,
        string displayName)
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
                PropertyAccessMode.Read
        };
    }

    private static PropertyValue CreatePropertyValue(
        double value)
    {
        return new PropertyValue(
            value,
            DateTimeOffset.UtcNow);
    }

    private static CorrelationId CreateDifferentCorrelationId(
        CorrelationId correlationId)
    {
        uint value =
            correlationId.Value == uint.MaxValue
                ? 1
                : correlationId.Value + 1;

        if (value == CorrelationId.None.Value)
        {
            value =
                1;
        }

        return new CorrelationId(
            value);
    }

    private static RuntimeProperty GetRuntimeProperty(
        RuntimeInstrument runtimeInstrument,
        PropertyId propertyId)
    {
        return runtimeInstrument.FindProperty(
                   propertyId)
               ?? throw new InvalidOperationException(
                   $"Runtime property "
                   + $"'{propertyId.Value}' was not found.");
    }

    private static void AssertAllPropertyCachesEmpty(
        RuntimeEndpoint runtimeEndpoint)
    {
        RuntimeInstrument runtimeInstrument =
            Assert.Single(
                runtimeEndpoint.Instruments);

        Assert.All(
            runtimeInstrument.Properties,
            runtimeProperty =>
                Assert.Null(
                    runtimeProperty.CurrentValue));
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

    private sealed class TestProtocolConnection
        : ITransportConnection
    {
        private readonly EndpointDescriptor _descriptor;

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

        public Func<ReadPropertyRequest, ProtocolMessage>?
            PropertyResponseFactory
        {
            get;
            set;
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

        private ProtocolMessage CreatePropertyResponse(
            ReadPropertyRequest request)
        {
            _readPropertyRequests.Add(
                request);

            Func<ReadPropertyRequest, ProtocolMessage>
                responseFactory =
                    PropertyResponseFactory
                    ?? throw new InvalidOperationException(
                        "No property-response factory was configured.");

            return responseFactory(
                request);
        }
    }
}