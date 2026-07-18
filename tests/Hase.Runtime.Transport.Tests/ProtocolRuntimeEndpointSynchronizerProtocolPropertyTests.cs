using Hase.Core.Domain.Data;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Protocol;
using Hase.Runtime.Runtime;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class
    ProtocolRuntimeEndpointSynchronizerProtocolPropertyTests
{
    private static readonly EndpointId EndpointId =
        new(
            "protocol-property-endpoint");

    private static readonly InstrumentId InstrumentId =
        new(
            "environment-sensor");

    private static readonly PropertyId TemperaturePropertyId =
        new(
            "environment.temperature");

    [Fact]
    public async Task SynchronizeAsync_ShouldPopulateReadablePropertyCache()
    {
        // Arrange
        EndpointDescriptor descriptor =
            CreateDescriptor();

        RuntimeEndpoint runtimeEndpoint =
            CreateRuntimeEndpoint(
                descriptor);

        var expectedValue =
            new PropertyValue(
                21.5,
                new DateTimeOffset(
                    2026,
                    7,
                    18,
                    10,
                    30,
                    0,
                    TimeSpan.Zero));

        var connection =
            new TestRuntimeProtocolConnection(
                descriptor,
                expectedValue);

        IRuntimeProtocolEndpointSynchronizer synchronizer =
            new ProtocolRuntimeEndpointSynchronizer(
                new EndpointDescriptorCompatibilityValidator());

        using var cancellationSource =
            new CancellationTokenSource();

        // Act
        await synchronizer.SynchronizeAsync(
            connection,
            runtimeEndpoint,
            cancellationSource.Token);

        // Assert
        Assert.Equal(
            2,
            connection.SendCount);

        Assert.Collection(
            connection.Requests,
            request =>
            {
                ReadEndpointDescriptorRequest descriptorRequest =
                    Assert.IsType<ReadEndpointDescriptorRequest>(
                        request);

                Assert.Equal(
                    EndpointId,
                    descriptorRequest.EndpointId);

                Assert.False(
                    descriptorRequest.CorrelationId.IsNone);
            },
            request =>
            {
                ReadPropertyRequest propertyRequest =
                    Assert.IsType<ReadPropertyRequest>(
                        request);

                Assert.Equal(
                    InstrumentId,
                    propertyRequest.InstrumentId);

                Assert.Equal(
                    TemperaturePropertyId,
                    propertyRequest.PropertyId);

                Assert.False(
                    propertyRequest.CorrelationId.IsNone);
            });

        Assert.All(
            connection.ReceivedCancellationTokens,
            token =>
                Assert.Equal(
                    cancellationSource.Token,
                    token));

        RuntimeInstrument runtimeInstrument =
            Assert.Single(
                runtimeEndpoint.Instruments);

        RuntimeProperty runtimeProperty =
            runtimeInstrument.FindProperty(
                TemperaturePropertyId)
            ?? throw new InvalidOperationException(
                "The temperature runtime property was not found.");

        PropertyValue actualValue =
            runtimeProperty.CurrentValue
            ?? throw new InvalidOperationException(
                "The temperature cache was not populated.");

        Assert.Equal(
            expectedValue.Value,
            actualValue.Value);

        Assert.Equal(
            expectedValue.TimestampUtc,
            actualValue.TimestampUtc);

        Assert.Equal(
            expectedValue.Quality,
            actualValue.Quality);
    }

    private static RuntimeEndpoint CreateRuntimeEndpoint(
        EndpointDescriptor descriptor)
    {
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
                    PropertyAccessMode.Read
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
                        "Protocol Property Endpoint",
                    Description =
                        "Endpoint used to test protocol-level property "
                        + "synchronization."
                }
        };
    }

    private sealed class TestRuntimeProtocolConnection
        : IRuntimeProtocolConnection
    {
        private readonly EndpointDescriptor _descriptor;

        private readonly PropertyValue _propertyValue;

        private readonly List<ProtocolMessage> _requests =
            new();

        private readonly List<CancellationToken>
            _receivedCancellationTokens =
                new();

        public TestRuntimeProtocolConnection(
            EndpointDescriptor descriptor,
            PropertyValue propertyValue)
        {
            _descriptor =
                descriptor
                ?? throw new ArgumentNullException(
                    nameof(descriptor));

            _propertyValue =
                propertyValue
                ?? throw new ArgumentNullException(
                    nameof(propertyValue));
        }

        public int SendCount =>
            _requests.Count;

        public IReadOnlyList<ProtocolMessage> Requests =>
            _requests;

        public IReadOnlyList<CancellationToken>
            ReceivedCancellationTokens =>
                _receivedCancellationTokens;

        public Task<ProtocolMessage> SendAsync(
            ProtocolMessage request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                request);

            cancellationToken.ThrowIfCancellationRequested();

            _requests.Add(
                request);

            _receivedCancellationTokens.Add(
                cancellationToken);

            ProtocolMessage response =
                request switch
                {
                    ReadEndpointDescriptorRequest descriptorRequest =>
                        new ReadEndpointDescriptorResponse(
                            descriptorRequest.CorrelationId,
                            ProtocolResult.Success,
                            _descriptor),

                    ReadPropertyRequest propertyRequest
                        when propertyRequest.InstrumentId
                            == InstrumentId
                        && propertyRequest.PropertyId
                            == TemperaturePropertyId =>
                        new ReadPropertyResponse(
                            propertyRequest.CorrelationId,
                            ProtocolResult.Success,
                            _propertyValue),

                    _ =>
                        throw new InvalidDataException(
                            $"Unsupported protocol request "
                            + $"'{request.MessageType}'.")
                };

            return Task.FromResult(
                response);
        }
    }
}