using Hase.Core.Domain.Data;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Runtime;

namespace Hase.Runtime.Tests.Integration;

public class RuntimeContextTests
{
    [Fact]
    public void AddEndpoint_ShouldCreateRuntimeEndpoint()
    {
        // Arrange
        var context = new RuntimeContext();

        var descriptor = new EndpointDescriptor(
            new EndpointId("Endpoint1"));

        // Act
        var endpoint = context.AddEndpoint(descriptor);

        // Assert
        Assert.Single(context.Endpoints);
        Assert.Same(endpoint, context.Endpoints[0]);
        Assert.Equal(descriptor.Id, endpoint.Descriptor.Id);
    }

    [Fact]
    public void PropertyUpdate_ShouldNotifyContextObserver()
    {
        // Arrange
        var propertyDescriptor = new PropertyDescriptor(
            new PropertyId("DDS.Frequency"),
            DescriptorPath.Parse("DDS.Frequency"),
            "Frequency",
            new NumericDataDescriptor(
                Quantities.Frequency,
                Units.Hertz));

        var instrumentDescriptor = new InstrumentDescriptor(
            new InstrumentId("DDS"),
            "DDS Generator",
            new InstrumentKind("FrequencyGenerator"))
        {
            Interface = new InstrumentInterface(
                properties: new[] { propertyDescriptor })
        };

        var endpointDescriptor = new EndpointDescriptor(
            new EndpointId("Endpoint1"),
            new[] { instrumentDescriptor });

        var context = new RuntimeContext();

        var endpoint = context.AddEndpoint(endpointDescriptor);

        var instrument = endpoint.FindInstrument(
            instrumentDescriptor.Id)!;

        var observer = new TestObserver();

        context.Subscribe(observer);

        // Act
        var value = new PropertyValue(
            1_000_000.0,
            DateTimeOffset.UtcNow);

        var updated = instrument.UpdatePropertyValue(
            DescriptorPath.Parse("DDS.Frequency"),
            value);

        // Assert
        Assert.True(updated);

        Assert.Equal(1, observer.NotificationCount);

        Assert.NotNull(observer.LastChange);

        Assert.Same(value, observer.LastChange!.CurrentValue);

        Assert.Null(observer.LastChange.PreviousValue);
    }

    [Fact]
    public void RuntimeGraph_ShouldHaveExpectedHierarchy()
    {
        // Arrange
        var propertyDescriptor = new PropertyDescriptor(
            new PropertyId("DDS.Frequency"),
            DescriptorPath.Parse("DDS.Frequency"),
            "Frequency",
            new NumericDataDescriptor(
                Quantities.Frequency,
                Units.Hertz));

        var instrumentDescriptor = new InstrumentDescriptor(
            new InstrumentId("DDS"),
            "DDS Generator",
            new InstrumentKind("FrequencyGenerator"))
        {
            Interface = new InstrumentInterface(
                properties: new[] { propertyDescriptor })
        };

        var endpointDescriptor = new EndpointDescriptor(
            new EndpointId("Endpoint1"),
            new[] { instrumentDescriptor });

        var context = new RuntimeContext();

        // Act
        var endpoint = context.AddEndpoint(endpointDescriptor);

        // Assert hierarchy

        Assert.Single(context.Children);

        var runtimeEndpoint = Assert.IsType<RuntimeEndpoint>(
            context.Children.Single());

        Assert.Same(context, runtimeEndpoint.Parent);

        Assert.Single(runtimeEndpoint.Children);

        var runtimeInstrument = Assert.IsType<RuntimeInstrument>(
            runtimeEndpoint.Children.Single());

        Assert.Same(runtimeEndpoint, runtimeInstrument.Parent);

        Assert.Single(runtimeInstrument.Properties);

        var runtimeProperty = runtimeInstrument.Properties.Single();

        Assert.Same(runtimeInstrument, runtimeProperty.Parent);

        Assert.Empty(runtimeProperty.Children);
    }

    private sealed class TestObserver : IPropertyValueObserver
    {
        public int NotificationCount { get; private set; }

        public PropertyValueChanged? LastChange { get; private set; }

        public void OnPropertyValueChanged(PropertyValueChanged change)
        {
            NotificationCount++;
            LastChange = change;
        }
    }
}