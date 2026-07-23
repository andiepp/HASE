using Hase.CompactProtocol;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Events;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.ProtocolExplorer.Scenarios;
using Xunit;

namespace Hase.ProtocolExplorer.Tests;

public sealed class PhysicalArduinoUnoCompactDescriptorFactoryTests
{
    [Fact]
    public void CreateCompactDefinition_ShouldRegisterButtonPressedEventMapping()
    {
        CompactEndpointDefinition definition =
            PhysicalArduinoUnoCompactDescriptorFactory
                .CreateCompactDefinition();

        CompactEventMapping mapping =
            Assert.Single(
                definition.EventMappings);

        Assert.Equal(
            PhysicalArduinoUnoCompactDescriptorFactory
                .ButtonPressedCompactEventId,
            mapping.CompactEventId);

        Assert.Equal(
            PhysicalArduinoUnoCompactDescriptorFactory
                .ControllerInstrumentId,
            mapping.InstrumentId);

        Assert.Equal(
            PhysicalArduinoUnoCompactDescriptorFactory
                .ButtonPressedEventPath,
            mapping.EventPath);

        Assert.Equal(
            CompactEventValueEncoding.None,
            mapping.Encoding);
    }

    [Fact]
    public void CreateDefinition_ShouldExposeButtonPressedRuntimeEvent()
    {
        EndpointDescriptor endpointDescriptor =
            PhysicalArduinoUnoCompactDescriptorFactory
                .CreateDefinition()
                .Materialize(
                    new EndpointId(
                        "arduino-uno-test"));

        InstrumentDescriptor controller =
            Assert.Single(
                endpointDescriptor.Instruments);

        Assert.Equal(
            PhysicalArduinoUnoCompactDescriptorFactory
                .ControllerInstrumentId,
            controller.Id);

        EventDescriptor runtimeEvent =
            Assert.Single(
                controller.Interface.Events);

        Assert.Equal(
            PhysicalArduinoUnoCompactDescriptorFactory
                .ButtonPressedEventPath,
            runtimeEvent.Path);

        Assert.Equal(
            "Button Pressed",
            runtimeEvent.DisplayName);
    }

    [Fact]
    public void CreateCompactDefinition_ShouldPreserveExistingPropertyMapping()
    {
        CompactEndpointDefinition definition =
            PhysicalArduinoUnoCompactDescriptorFactory
                .CreateCompactDefinition();

        CompactPropertyMapping mapping =
            Assert.Single(
                definition.PropertyMappings);

        Assert.Equal(
            PhysicalArduinoUnoCompactDescriptorFactory
                .BuiltInLedStateCompactPropertyId,
            mapping.CompactPropertyId);

        Assert.Equal(
            PhysicalArduinoUnoCompactDescriptorFactory
                .ControllerInstrumentId,
            mapping.InstrumentId);

        Assert.Equal(
            PhysicalArduinoUnoCompactDescriptorFactory
                .BuiltInLedStatePropertyId,
            mapping.PropertyId);

        Assert.Equal(
            CompactPropertyValueEncoding.Boolean,
            mapping.Encoding);
    }
}