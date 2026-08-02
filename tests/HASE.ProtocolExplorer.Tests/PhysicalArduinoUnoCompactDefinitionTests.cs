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
            definition.PropertyMappings.Single(candidate =>
                candidate.CompactPropertyId ==
                PhysicalArduinoUnoCompactDescriptorFactory
                    .BuiltInLedStateCompactPropertyId);

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

    [Fact]
    public void CreateCompactDefinition_ShouldContainAnalogVoltageMapping()
    {
        CompactEndpointDefinition definition =
            PhysicalArduinoUnoCompactDescriptorFactory
                .CreateCompactDefinition();

        CompactPropertyMapping mapping =
            definition.PropertyMappings.Single(candidate =>
                candidate.CompactPropertyId ==
                PhysicalArduinoUnoCompactDescriptorFactory
                    .AnalogInputVoltageCompactPropertyId);

        Assert.Equal(
            PhysicalArduinoUnoCompactDescriptorFactory
                .AnalogInputVoltagePropertyId,
            mapping.PropertyId);
        Assert.Equal(
            CompactPropertyValueEncoding.Unsigned16LittleEndianMillivolts,
            mapping.Encoding);
    }

    [Fact]
    public void CreateCompactDefinition_ShouldUseDescriptorVersionTwo()
    {
        CompactEndpointDefinition definition =
            PhysicalArduinoUnoCompactDescriptorFactory
                .CreateCompactDefinition();

        Assert.Equal(2, definition.DescriptorReference.Version);
    }

    [Fact]
    public void CreateDefinition_ShouldDescribeReadOnlyA0VoltageInVolts()
    {
        var descriptor = PhysicalArduinoUnoCompactDescriptorFactory
            .CreateDefinition();
        Hase.Core.Domain.Properties.PropertyDescriptor property =
            descriptor.Instruments.Single()
                .Interface.Properties.Single(candidate =>
                    candidate.Id ==
                    PhysicalArduinoUnoCompactDescriptorFactory
                        .AnalogInputVoltagePropertyId);
        Hase.Core.Domain.Data.NumericDataDescriptor numeric =
            Assert.IsType<Hase.Core.Domain.Data.NumericDataDescriptor>(
                property.Data);

        Assert.Equal(
            Hase.Core.Domain.Properties.PropertyAccessMode.Read,
            property.AccessMode);
        Assert.Equal(Hase.Core.Domain.Data.Units.Volt, numeric.NativeUnit);
        Assert.Equal(0.0, numeric.Range!.Minimum);
        Assert.Equal(5.0, numeric.Range.Maximum);
        Assert.Equal(
            5.0 / 1023.0,
            numeric.Resolution!.Value,
            precision: 10);
    }
}
