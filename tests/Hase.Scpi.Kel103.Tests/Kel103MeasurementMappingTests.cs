using System.Globalization;

namespace Hase.Scpi.Kel103.Tests;

public sealed class Kel103MeasurementMappingTests
{
    [Theory]
    [InlineData(0, ":MEASure:VOLTage?", "measured-voltage", "Measurement.Voltage", "V")]
    [InlineData(1, ":MEASure:CURRent?", "measured-current", "Measurement.Current", "A")]
    [InlineData(2, ":MEASure:POWer?", "measured-power", "Measurement.Power", "W")]
    public void All_ContainsExactMappings(int index, string query, string id, string path, string unit)
    {
        Kel103MeasurementMapping mapping = Kel103MeasurementMapping.All[index];
        Assert.Equal(query, mapping.Query);
        Assert.Equal(id, mapping.PropertyId.Value);
        Assert.Equal(path, mapping.PropertyPath.ToString());
        Assert.Equal(unit, mapping.UnitSymbol);
        Assert.Equal((Kel103Measurement)index, mapping.Measurement);
    }

    [Theory]
    [InlineData(0, "0.0000V", "0.0000")]
    [InlineData(0, "9.8864V", "9.8864")]
    [InlineData(1, "0.1000A", "0.1000")]
    [InlineData(2, "0.9893W", "0.9893")]
    public void ParseResponse_AcceptsPhysicallyObservedForms(int index, string response, string expected)
    {
        Assert.Equal(
            decimal.Parse(expected, CultureInfo.InvariantCulture),
            Kel103MeasurementMapping.All[index].ParseResponse(response));
    }

    [Theory]
    [InlineData("-0.1V")]
    [InlineData("+0.1V")]
    [InlineData("1V ")]
    [InlineData(" 1V")]
    [InlineData("1A")]
    [InlineData("1e1V")]
    [InlineData("NaNV")]
    [InlineData("1VV")]
    [InlineData("1V\n")]
    public void ParseResponse_RejectsUnsupportedFormsWithoutEcho(string response)
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            Kel103MeasurementMapping.Voltage.ParseResponse(response));
        Assert.DoesNotContain(response, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseResponse_IsCultureInvariant()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            Assert.Equal(1.25m, Kel103MeasurementMapping.Voltage.ParseResponse("1.25V"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
