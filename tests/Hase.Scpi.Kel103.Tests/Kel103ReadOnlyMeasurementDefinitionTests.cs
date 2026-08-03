using Hase.Core.Domain.Data;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Scpi.Kel103.Tests;

public sealed class Kel103ReadOnlyMeasurementDefinitionTests
{
    [Fact]
    public void Reference_PreservesIdAndAdvancesToVersionTwo()
    {
        Assert.Equal(Kel103IdentityDefinition.Reference.Id, Kel103ReadOnlyMeasurementDefinition.Reference.Id);
        Assert.Equal((ushort)2, Kel103ReadOnlyMeasurementDefinition.Reference.Version);
        Assert.Equal((ushort)1, Kel103IdentityDefinition.Reference.Version);
    }

    [Fact]
    public void Definition_RetainsIdentityAndAddsThreeReadOnlyMeasurements()
    {
        var instrument = Assert.Single(Kel103ReadOnlyMeasurementDefinition.EndpointDefinition.Instruments);
        Assert.Equal(5, instrument.Interface.Properties.Count);
        Assert.Equal(
            new[] { "product-identity", "firmware-version", "measured-voltage", "measured-current", "measured-power" },
            instrument.Interface.Properties.Select(property => property.Id.Value));
        Assert.All(instrument.Interface.Properties, property => Assert.Equal(PropertyAccessMode.Read, property.AccessMode));
        Assert.Empty(instrument.Interface.Commands);
        Assert.Empty(instrument.Interface.Events);
    }

    [Theory]
    [InlineData(2, "electric-voltage", "volt", "V")]
    [InlineData(3, "electric-current", "ampere", "A")]
    [InlineData(4, "power", "watt", "W")]
    public void MeasurementProperties_HaveExactUnitsAndNoUnverifiedBounds(
        int index, string quantityId, string unitId, string symbol)
    {
        var instrument = Assert.Single(Kel103ReadOnlyMeasurementDefinition.EndpointDefinition.Instruments);
        var numeric = Assert.IsType<NumericDataDescriptor>(instrument.Interface.Properties[index].Data);
        Assert.Equal(quantityId, numeric.Quantity.Id);
        Assert.Equal(unitId, numeric.NativeUnit.Id);
        Assert.Equal(symbol, numeric.NativeUnit.Symbol);
        Assert.Null(numeric.Range);
        Assert.Null(numeric.Resolution);
    }

    [Fact]
    public void Materialize_UsesExternalEndpointIdentity()
    {
        var endpointId = new EndpointId("configured-endpoint");
        Assert.Same(
            endpointId,
            Kel103ReadOnlyMeasurementDefinition.EndpointDefinition.Materialize(endpointId).Id);
    }
}
