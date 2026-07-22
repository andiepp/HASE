using Hase.CompactProtocol;
using Hase.ProtocolExplorer.Scenarios;
using Xunit;

namespace Hase.ProtocolExplorer.Tests;

public sealed class PhysicalArduinoUnoCompactDefinitionTests
{
    [Fact]
    public void CreateCompactDefinition_ShouldUseExactDescriptorReference()
    {
        // Act
        CompactEndpointDefinition definition =
            PhysicalArduinoUnoCompactDescriptorFactory
                .CreateCompactDefinition();

        // Assert
        Assert.Same(
            PhysicalArduinoUnoCompactDescriptorFactory
                .DescriptorReference,
            definition.DescriptorReference);
    }

    [Fact]
    public void CreateCompactDefinition_ShouldContainLedStateMapping()
    {
        // Act
        CompactEndpointDefinition definition =
            PhysicalArduinoUnoCompactDescriptorFactory
                .CreateCompactDefinition();

        // Assert
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