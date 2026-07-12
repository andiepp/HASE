using Hase.Core.Domain.Data;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Protocol;
using Hase.Runtime.Protocol;
using Hase.Runtime.Runtime;

namespace Hase.Runtime.Tests.Protocol;

public class RuntimeProtocolDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_ShouldReturnDiscoverResponse()
    {
        // Arrange
        var context = new RuntimeContext();

        var endpointDescriptor = new EndpointDescriptor(
            new EndpointId("Endpoint1"));

        RuntimeEndpoint endpoint =
            context.AddEndpoint(endpointDescriptor);

        var dispatcher =
            new RuntimeProtocolDispatcher(endpoint);

        var request =
            new DiscoverRequest(CorrelationId.None);

        // Act
        DiscoverResponse response =
            await dispatcher.DispatchAsync(request);

        // Assert
        Assert.Equal(
            request.CorrelationId,
            response.CorrelationId);

        Assert.Equal(
            endpointDescriptor.Id,
            response.EndpointId);

        Assert.Empty(response.InstrumentIds);
    }

    [Fact]
    public async Task DispatchAsync_ReadEndpointDescriptor_ShouldReturnDescriptor()
    {
        // Arrange
        var context = new RuntimeContext();

        var descriptor = new EndpointDescriptor(
            new EndpointId("Endpoint1"));

        RuntimeEndpoint endpoint =
            context.AddEndpoint(descriptor);

        var dispatcher =
            new RuntimeProtocolDispatcher(endpoint);

        var request =
            new ReadEndpointDescriptorRequest(
                CorrelationId.None,
                descriptor.Id);

        // Act
        ReadEndpointDescriptorResponse response =
            await dispatcher.DispatchAsync(request);

        // Assert
        Assert.Equal(
            request.CorrelationId,
            response.CorrelationId);

        Assert.Equal(
            ProtocolResult.Success,
            response.Result);

        Assert.Same(
            descriptor,
            response.Descriptor);
    }

    [Fact]
    public async Task DispatchAsync_ReadProperty_ShouldReturnCurrentValue()
    {
        // Arrange
        var propertyDescriptor =
            new PropertyDescriptor(
                new PropertyId("DDS.Frequency"),
                DescriptorPath.Parse("DDS.Frequency"),
                "Frequency",
                new NumericDataDescriptor(
                    Quantities.Frequency,
                    Units.Hertz));

        var instrumentDescriptor =
            new InstrumentDescriptor(
                new InstrumentId("DDS"),
                "DDS Generator",
                new InstrumentKind("FrequencyGenerator"))
            {
                Interface = new InstrumentInterface(
                    properties: new[] { propertyDescriptor })
            };

        var endpointDescriptor =
            new EndpointDescriptor(
                new EndpointId("Endpoint1"),
                new[] { instrumentDescriptor });

        var context = new RuntimeContext();

        RuntimeEndpoint endpoint =
            context.AddEndpoint(endpointDescriptor);

        RuntimeInstrument instrument =
            endpoint.FindInstrument(
                instrumentDescriptor.Id)!;

        var expectedValue =
            new PropertyValue(
                1_000_000.0,
                DateTimeOffset.UtcNow);

        bool updated =
            instrument.UpdatePropertyValue(
                propertyDescriptor.Path,
                expectedValue);

        Assert.True(updated);

        var dispatcher =
            new RuntimeProtocolDispatcher(endpoint);

        var request =
            new ReadPropertyRequest(
                CorrelationId.None,
                instrumentDescriptor.Id,
                propertyDescriptor.Id);

        // Act
        ReadPropertyResponse response =
            await dispatcher.DispatchAsync(request);

        // Assert
        Assert.Equal(
            request.CorrelationId,
            response.CorrelationId);

        Assert.Equal(
            ProtocolResult.Success,
            response.Result);

        Assert.Same(
            expectedValue,
            response.PropertyValue);
    }
}