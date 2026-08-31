using Hase.Core.Domain.Data;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;

namespace Hase.Mcnf.RfLab.Tests;

public sealed class RfLabDefinitionTests
{
    [Fact]
    public void References_ShareOneDescriptorIdWithAscendingVersions()
    {
        Assert.Equal("rflab-signal-lab", RfLabReadOnlyDefinition.Reference.Id.Value);
        Assert.Equal(1, RfLabReadOnlyDefinition.Reference.Version);
        Assert.Equal(
            RfLabReadOnlyDefinition.Reference.Id,
            RfLabControlledSignalDefinition.Reference.Id);
        Assert.Equal(2, RfLabControlledSignalDefinition.Reference.Version);
    }

    [Fact]
    public void ReadOnlyDefinition_ExposesOnlyReadableStateAndNoCommands()
    {
        InstrumentDescriptor instrument =
            RfLabReadOnlyDefinition.EndpointDefinition.Instruments.Single();

        Assert.Equal(6, instrument.Interface.Properties.Count);
        Assert.Empty(instrument.Interface.Commands);
        Assert.Empty(instrument.Interface.Events);
        Assert.All(
            instrument.Interface.Properties,
            property => Assert.Equal(PropertyAccessMode.Read, property.AccessMode));
    }

    [Fact]
    public void ReadOnlyDefinition_DescribesTheCharacterizedMeasurements()
    {
        InstrumentDescriptor instrument =
            RfLabReadOnlyDefinition.EndpointDefinition.Instruments.Single();

        PropertyDescriptor level = instrument.Interface.Properties
            .Single(property => property.Id == RfLabProperties.SensorLevel);
        var levelData = Assert.IsType<NumericDataDescriptor>(level.Data);
        Assert.Equal("dB", levelData.NativeUnit.Symbol);
        Assert.Equal(new ValueRange(-70.0, 10.0), levelData.Range);

        PropertyDescriptor voltage = instrument.Interface.Properties
            .Single(property => property.Id == RfLabProperties.SensorVoltage);
        var voltageData = Assert.IsType<NumericDataDescriptor>(voltage.Data);
        Assert.Equal("mV", voltageData.NativeUnit.Symbol);
        Assert.Equal(new ValueRange(0.0, 2560.0), voltageData.Range);
    }

    [Fact]
    public void ControlledDefinition_ExtendsTheReadOnlyDefinitionAdditively()
    {
        InstrumentDescriptor readOnly =
            RfLabReadOnlyDefinition.EndpointDefinition.Instruments.Single();
        InstrumentDescriptor controlled =
            RfLabControlledSignalDefinition.EndpointDefinition.Instruments.Single();

        Assert.Equal(readOnly.Id, controlled.Id);
        Assert.Equal(
            readOnly.Interface.Properties.Count + RfLabTargetMapping.All.Count,
            controlled.Interface.Properties.Count);

        // The read-only properties remain an unchanged prefix.
        for (int index = 0; index < readOnly.Interface.Properties.Count; index++)
        {
            Assert.Equal(
                readOnly.Interface.Properties[index],
                controlled.Interface.Properties[index]);
        }
    }

    [Fact]
    public void ControlledDefinition_DeclaresEveryTargetWritableWithItsCharacterizedRange()
    {
        InstrumentDescriptor controlled =
            RfLabControlledSignalDefinition.EndpointDefinition.Instruments.Single();

        foreach (RfLabTargetMapping mapping in RfLabTargetMapping.All)
        {
            PropertyDescriptor property = controlled.Interface.Properties
                .Single(candidate => candidate.Id == mapping.PropertyId);
            Assert.Equal(PropertyAccessMode.ReadWrite, property.AccessMode);
            Assert.Equal(mapping.PropertyPath, property.Path);
            var data = Assert.IsType<NumericDataDescriptor>(property.Data);
            Assert.Equal(new ValueRange(mapping.Minimum, mapping.Maximum), data.Range);
            Assert.Equal(mapping.Unit.Id, data.NativeUnit.Id);
        }
    }

    [Fact]
    public void ControlledDefinition_DeclaresEveryCommandParameterless()
    {
        InstrumentDescriptor controlled =
            RfLabControlledSignalDefinition.EndpointDefinition.Instruments.Single();

        Assert.Equal(RfLabCommandMapping.All.Count, controlled.Interface.Commands.Count);
        Assert.All(
            controlled.Interface.Commands,
            command => Assert.Null(command.Argument));

        foreach (RfLabCommandMapping mapping in RfLabCommandMapping.All)
        {
            Assert.Contains(
                controlled.Interface.Commands,
                command => command.Path == mapping.CommandPath);
        }
    }

    [Fact]
    public void Definitions_AreImmutableSingletons()
    {
        Assert.Same(
            RfLabReadOnlyDefinition.EndpointDefinition,
            RfLabReadOnlyDefinition.EndpointDefinition);
        Assert.Same(
            RfLabControlledSignalDefinition.EndpointDefinition,
            RfLabControlledSignalDefinition.EndpointDefinition);
    }
}
