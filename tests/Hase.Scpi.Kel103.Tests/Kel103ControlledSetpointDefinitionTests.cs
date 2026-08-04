using Hase.Core.Domain.Data;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Scpi.Kel103.Tests;

public sealed class Kel103ControlledSetpointDefinitionTests
{
    [Fact]
    public void Reference_PreservesIdAndAdvancesToVersionFour()
    {
        Assert.Equal(Kel103IdentityDefinition.Reference.Id, Kel103ControlledSetpointDefinition.Reference.Id);
        Assert.Equal((ushort)4, Kel103ControlledSetpointDefinition.Reference.Version);
        Assert.Equal((ushort)3, Kel103OperatingStateDefinition.Reference.Version);
        Assert.Equal((ushort)2, Kel103ReadOnlyMeasurementDefinition.Reference.Version);
        Assert.Equal((ushort)1, Kel103IdentityDefinition.Reference.Version);
    }

    [Fact]
    public void Definition_RetainsPropertiesAndMakesOnlyFourTargetsWritable()
    {
        var properties = Assert.Single(
            Kel103ControlledSetpointDefinition.EndpointDefinition.Instruments).Interface.Properties;

        Assert.Equal(11, properties.Count);
        Assert.Equal(
            new[]
            {
                "product-identity",
                "firmware-version",
                "measured-voltage",
                "measured-current",
                "measured-power",
                "operating-mode",
                "input-enabled",
                "target-voltage",
                "target-current",
                "target-resistance",
                "target-power"
            },
            properties.Select(property => property.Id.Value));
        Assert.All(
            properties.Take(7),
            property => Assert.Equal(PropertyAccessMode.Read, property.AccessMode));
        Assert.All(
            properties.Skip(7),
            property => Assert.Equal(PropertyAccessMode.ReadWrite, property.AccessMode));
    }

    [Theory]
    [InlineData(7, "Target.Voltage", "electric-voltage", "volt", "V", 0.1, 120.0)]
    [InlineData(8, "Target.Current", "electric-current", "ampere", "A", 0.0, 30.0)]
    [InlineData(9, "Target.Resistance", "electrical-resistance", "ohm", "OHM", 0.05, 7500.0)]
    [InlineData(10, "Target.Power", "power", "watt", "W", 0.0, 300.0)]
    public void WritableTargets_PreserveExactPathsUnitsRangesAndAbsentResolution(
        int index,
        string path,
        string quantityId,
        string unitId,
        string symbol,
        double minimum,
        double maximum)
    {
        var property = Assert.Single(
            Kel103ControlledSetpointDefinition.EndpointDefinition.Instruments).Interface.Properties[index];
        var numeric = Assert.IsType<NumericDataDescriptor>(property.Data);

        Assert.Equal(DescriptorPath.Parse(path), property.Path);
        Assert.Equal(quantityId, numeric.Quantity.Id);
        Assert.Equal(unitId, numeric.NativeUnit.Id);
        Assert.Equal(symbol, numeric.NativeUnit.Symbol);
        Assert.Equal(new ValueRange(minimum, maximum), numeric.Range);
        Assert.Null(numeric.Resolution);
    }

    [Fact]
    public void Definition_ExposesExactlyFiveParameterlessModeSelectionCommands()
    {
        var instrument = Assert.Single(Kel103ControlledSetpointDefinition.EndpointDefinition.Instruments);

        Assert.Equal(
            new[]
            {
                "Mode.SelectConstantCurrent",
                "Mode.SelectConstantVoltage",
                "Mode.SelectConstantResistance",
                "Mode.SelectConstantPower",
                "Mode.SelectShortCircuit"
            }.Select(DescriptorPath.Parse),
            instrument.Interface.Commands.Select(command => command.Path));
        Assert.All(instrument.Interface.Commands, command => Assert.Null(command.Argument));
        Assert.DoesNotContain(
            instrument.Interface.Commands,
            command => command.Path == DescriptorPath.Parse("Input.Activate"));
        Assert.DoesNotContain(
            instrument.Interface.Commands,
            command => command.Path == DescriptorPath.Parse("Input.Deactivate"));
        Assert.DoesNotContain(
            instrument.Interface.Commands,
            command => command.Path == DescriptorPath.Parse("ShortCircuit.Activate"));
        Assert.Empty(instrument.Interface.Events);
    }

    [Fact]
    public void VersionThreeDefinition_RemainsReadOnlyAndCommandFree()
    {
        var instrument = Assert.Single(Kel103OperatingStateDefinition.EndpointDefinition.Instruments);

        Assert.Equal(11, instrument.Interface.Properties.Count);
        Assert.All(
            instrument.Interface.Properties,
            property => Assert.Equal(PropertyAccessMode.Read, property.AccessMode));
        Assert.Empty(instrument.Interface.Commands);
        Assert.Empty(instrument.Interface.Events);
    }

    [Fact]
    public void Materialize_UsesExternalEndpointIdentity()
    {
        var endpointId = new EndpointId("configured-endpoint");

        Assert.Same(
            endpointId,
            Kel103ControlledSetpointDefinition.EndpointDefinition.Materialize(endpointId).Id);
    }
}
