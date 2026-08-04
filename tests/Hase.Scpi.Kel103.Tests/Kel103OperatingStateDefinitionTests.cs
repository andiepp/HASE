using Hase.Core.Domain.Data;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Scpi.Kel103.Tests;

public sealed class Kel103OperatingStateDefinitionTests
{
    [Fact]
    public void Reference_PreservesIdAndAdvancesToVersionThree()
    {
        Assert.Equal(Kel103IdentityDefinition.Reference.Id, Kel103OperatingStateDefinition.Reference.Id);
        Assert.Equal((ushort)3, Kel103OperatingStateDefinition.Reference.Version);
        Assert.Equal((ushort)2, Kel103ReadOnlyMeasurementDefinition.Reference.Version);
        Assert.Equal((ushort)1, Kel103IdentityDefinition.Reference.Version);
    }

    [Fact]
    public void Definition_RetainsVersionTwoPropertiesAndAddsSixReadOnlyStateProperties()
    {
        var instrument = Assert.Single(Kel103OperatingStateDefinition.EndpointDefinition.Instruments);

        Assert.Equal(11, instrument.Interface.Properties.Count);
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
            instrument.Interface.Properties.Select(property => property.Id.Value));
        Assert.All(
            instrument.Interface.Properties,
            property => Assert.Equal(PropertyAccessMode.Read, property.AccessMode));
        Assert.Empty(instrument.Interface.Commands);
        Assert.Empty(instrument.Interface.Events);
    }

    [Fact]
    public void OperatingStateProperties_HaveExactPathsAndDataTypes()
    {
        var properties = Assert.Single(
            Kel103OperatingStateDefinition.EndpointDefinition.Instruments).Interface.Properties;

        Assert.Equal(DescriptorPath.Parse("Operating.Mode"), properties[5].Path);
        Assert.IsType<StringDataDescriptor>(properties[5].Data);
        Assert.Equal(DescriptorPath.Parse("Input.Enabled"), properties[6].Path);
        Assert.IsType<BooleanDataDescriptor>(properties[6].Data);
    }

    [Theory]
    [InlineData(7, "Target.Voltage", "electric-voltage", "volt", "V", 0.1, 120.0)]
    [InlineData(8, "Target.Current", "electric-current", "ampere", "A", 0.0, 30.0)]
    [InlineData(9, "Target.Resistance", "electrical-resistance", "ohm", "OHM", 0.05, 7500.0)]
    [InlineData(10, "Target.Power", "power", "watt", "W", 0.0, 300.0)]
    public void TargetProperties_HaveExactPathsUnitsAndCharacterizedRanges(
        int index,
        string path,
        string quantityId,
        string unitId,
        string symbol,
        double minimum,
        double maximum)
    {
        var property = Assert.Single(
            Kel103OperatingStateDefinition.EndpointDefinition.Instruments).Interface.Properties[index];
        var numeric = Assert.IsType<NumericDataDescriptor>(property.Data);

        Assert.Equal(DescriptorPath.Parse(path), property.Path);
        Assert.Equal(quantityId, numeric.Quantity.Id);
        Assert.Equal(unitId, numeric.NativeUnit.Id);
        Assert.Equal(symbol, numeric.NativeUnit.Symbol);
        Assert.Equal(new ValueRange(minimum, maximum), numeric.Range);
        Assert.Null(numeric.Resolution);
    }

    [Fact]
    public void VersionTwoDefinition_RemainsUnchanged()
    {
        var instrument = Assert.Single(Kel103ReadOnlyMeasurementDefinition.EndpointDefinition.Instruments);

        Assert.Equal(5, instrument.Interface.Properties.Count);
        Assert.All(
            instrument.Interface.Properties,
            property => Assert.Equal(PropertyAccessMode.Read, property.AccessMode));
        Assert.Empty(instrument.Interface.Commands);
    }

    [Fact]
    public void Materialize_UsesExternalEndpointIdentity()
    {
        var endpointId = new EndpointId("configured-endpoint");

        Assert.Same(
            endpointId,
            Kel103OperatingStateDefinition.EndpointDefinition.Materialize(endpointId).Id);
    }
}
