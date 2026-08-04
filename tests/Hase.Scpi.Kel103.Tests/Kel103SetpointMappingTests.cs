using System.Globalization;

namespace Hase.Scpi.Kel103.Tests;

public sealed class Kel103SetpointMappingTests
{
    [Theory]
    [InlineData(0, Kel103Setpoint.Voltage, "target-voltage", "Target.Voltage", ":VOLTage?", "V", "0.1", "120.0")]
    [InlineData(1, Kel103Setpoint.Current, "target-current", "Target.Current", ":CURRent?", "A", "0.0", "30.0")]
    [InlineData(2, Kel103Setpoint.Resistance, "target-resistance", "Target.Resistance", ":RESistance?", "OHM", "0.05", "7500.0")]
    [InlineData(3, Kel103Setpoint.Power, "target-power", "Target.Power", ":POWer?", "W", "0.0", "300.0")]
    public void Mapping_HasExactCharacterizedMetadata(
        int index,
        Kel103Setpoint setpoint,
        string propertyId,
        string propertyPath,
        string query,
        string unit,
        string minimum,
        string maximum)
    {
        Kel103SetpointMapping mapping = Kel103SetpointMapping.All[index];

        Assert.Equal(setpoint, mapping.Setpoint);
        Assert.Equal(propertyId, mapping.PropertyId.Value);
        Assert.Equal(propertyPath, mapping.PropertyPath.ToString());
        Assert.Equal(query, mapping.Query);
        Assert.Equal(unit, mapping.UnitSymbol);
        Assert.Equal(decimal.Parse(minimum, CultureInfo.InvariantCulture), mapping.Minimum);
        Assert.Equal(decimal.Parse(maximum, CultureInfo.InvariantCulture), mapping.Maximum);
    }

    [Theory]
    [InlineData(0, Kel103OperatingMode.ConstantVoltage)]
    [InlineData(1, Kel103OperatingMode.ConstantCurrent)]
    [InlineData(2, Kel103OperatingMode.ConstantResistance)]
    [InlineData(3, Kel103OperatingMode.ConstantPower)]
    public void Mapping_HasExactCharacterizedAssociatedMode(
        int index,
        Kel103OperatingMode expected)
    {
        Assert.Equal(expected, Kel103SetpointMapping.All[index].AssociatedMode);
    }

    [Theory]
    [InlineData(0, "0.1", ":VOLTage 0.1V")]
    [InlineData(0, "1.2500", ":VOLTage 1.25V")]
    [InlineData(0, "120.0", ":VOLTage 120V")]
    [InlineData(1, "0.0", ":CURRent 0A")]
    [InlineData(1, "0.1000", ":CURRent 0.1A")]
    [InlineData(1, "30.0", ":CURRent 30A")]
    [InlineData(2, "0.05", ":RESistance 0.05OHM")]
    [InlineData(2, "100.000", ":RESistance 100OHM")]
    [InlineData(2, "7500.0", ":RESistance 7500OHM")]
    [InlineData(3, "0.0", ":POWer 0W")]
    [InlineData(3, "0.1250", ":POWer 0.125W")]
    [InlineData(3, "300.0", ":POWer 300W")]
    public void FormatSetterCommand_UsesExactInvariantCommandText(
        int index,
        string value,
        string expected)
    {
        Assert.Equal(
            expected,
            Kel103SetpointMapping.All[index].FormatSetterCommand(
                decimal.Parse(value, CultureInfo.InvariantCulture)));
    }

    [Theory]
    [InlineData(0, "0.0999")]
    [InlineData(0, "120.01")]
    [InlineData(1, "-0.0001")]
    [InlineData(1, "30.001")]
    [InlineData(2, "0.0499")]
    [InlineData(2, "7500.1")]
    [InlineData(3, "-0.0001")]
    [InlineData(3, "300.01")]
    public void FormatSetterCommand_RejectsValuesOutsideCharacterizedRange(
        int index,
        string value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Kel103SetpointMapping.All[index].FormatSetterCommand(
                decimal.Parse(value, CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void FormatSetterCommand_IsInvariantUnderCommaDecimalCulture()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            Assert.Equal(
                ":VOLTage 1.25V",
                Kel103SetpointMapping.Voltage.FormatSetterCommand(1.25m));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void FormatSetterCommand_NeverEmitsDeferredSyntax()
    {
        string[] commands =
        [
            Kel103SetpointMapping.Voltage.FormatSetterCommand(1.25m),
            Kel103SetpointMapping.Current.FormatSetterCommand(0.1m),
            Kel103SetpointMapping.Resistance.FormatSetterCommand(100m),
            Kel103SetpointMapping.Power.FormatSetterCommand(0.125m)
        ];

        Assert.All(commands, command =>
        {
            string valueAndUnit = command[(command.IndexOf(' ') + 1)..];
            Assert.DoesNotContain("MIN", command, StringComparison.Ordinal);
            Assert.DoesNotContain("MAX", command, StringComparison.Ordinal);
            Assert.DoesNotContain(',', valueAndUnit);
            Assert.DoesNotContain('E', valueAndUnit);
            Assert.DoesNotContain('e', valueAndUnit);
        });
    }

    [Theory]
    [InlineData(0, "0.1000V", "0.1000")]
    [InlineData(0, "120.00V", "120.00")]
    [InlineData(1, "0.0000A", "0.0000")]
    [InlineData(1, "30.000A", "30.000")]
    [InlineData(2, "0.0500OHM", "0.0500")]
    [InlineData(2, "7500.0OHM", "7500.0")]
    [InlineData(3, "0.0000W", "0.0000")]
    [InlineData(3, "300.00W", "300.00")]
    public void ParseResponse_AcceptsExactBoundaryValues(
        int index,
        string response,
        string expected)
    {
        Assert.Equal(
            decimal.Parse(expected, CultureInfo.InvariantCulture),
            Kel103SetpointMapping.All[index].ParseResponse(response));
    }

    [Theory]
    [InlineData(0, "0.0999V")]
    [InlineData(0, "120.01V")]
    [InlineData(1, "-0.0001A")]
    [InlineData(1, "30.001A")]
    [InlineData(2, "0.0499OHM")]
    [InlineData(2, "7500.1OHM")]
    [InlineData(3, "-0.0001W")]
    [InlineData(3, "300.01W")]
    public void ParseResponse_RejectsValuesOutsideCharacterizedRange(
        int index,
        string response)
    {
        Assert.Throws<InvalidDataException>(() =>
            Kel103SetpointMapping.All[index].ParseResponse(response));
    }

    [Theory]
    [InlineData(0, "1")]
    [InlineData(0, "1A")]
    [InlineData(0, "1v")]
    [InlineData(0, "1 V")]
    [InlineData(0, " 1V")]
    [InlineData(0, "1V ")]
    [InlineData(0, "1,0V")]
    [InlineData(0, "1E0V")]
    [InlineData(2, "OHM")]
    [InlineData(2, "1Ohm")]
    public void ParseResponse_RejectsUnsupportedSyntax(int index, string response)
    {
        Assert.Throws<InvalidDataException>(() =>
            Kel103SetpointMapping.All[index].ParseResponse(response));
    }

    [Fact]
    public void ParseResponse_IsInvariantUnderCommaDecimalCulture()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            Assert.Equal(1.25m, Kel103SetpointMapping.Voltage.ParseResponse("1.25V"));
            Assert.Throws<InvalidDataException>(() =>
                Kel103SetpointMapping.Voltage.ParseResponse("1,25V"));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void ParseResponse_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Kel103SetpointMapping.Voltage.ParseResponse(null!));
    }
}
