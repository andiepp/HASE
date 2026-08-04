namespace Hase.Scpi.Kel103.Tests;

public sealed class Kel103OperatingModeMappingTests
{
    [Fact]
    public void Mapping_HasExactPropertyAndQueryMetadata()
    {
        Assert.Equal("operating-mode", Kel103OperatingModeMapping.PropertyId.Value);
        Assert.Equal("Operating.Mode", Kel103OperatingModeMapping.PropertyPath.ToString());
        Assert.Equal(":FUNCtion?", Kel103OperatingModeMapping.Query);
    }

    [Theory]
    [InlineData("CC", Kel103OperatingMode.ConstantCurrent)]
    [InlineData("CV", Kel103OperatingMode.ConstantVoltage)]
    [InlineData("CR", Kel103OperatingMode.ConstantResistance)]
    [InlineData("CW", Kel103OperatingMode.ConstantPower)]
    [InlineData("SHORt", Kel103OperatingMode.ShortCircuit)]
    public void ParseResponse_RecognizesExactCharacterizedTokens(
        string response,
        Kel103OperatingMode expected)
    {
        Assert.Equal(expected, Kel103OperatingModeMapping.ParseResponse(response));
    }

    [Theory]
    [InlineData("")]
    [InlineData("cc")]
    [InlineData("SHORT")]
    [InlineData("SHORt ")]
    [InlineData(" CC")]
    [InlineData("CV\n")]
    public void ParseResponse_RejectsUnsupportedSyntax(string response)
    {
        Assert.Throws<InvalidDataException>(() =>
            Kel103OperatingModeMapping.ParseResponse(response));
    }

    [Fact]
    public void ParseResponse_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Kel103OperatingModeMapping.ParseResponse(null!));
    }

    [Theory]
    [InlineData(Kel103OperatingMode.ConstantCurrent, "CC")]
    [InlineData(Kel103OperatingMode.ConstantVoltage, "CV")]
    [InlineData(Kel103OperatingMode.ConstantResistance, "CR")]
    [InlineData(Kel103OperatingMode.ConstantPower, "CW")]
    [InlineData(Kel103OperatingMode.ShortCircuit, "SHORT")]
    public void ToNormalizedValue_ReturnsStableRuntimeDisplay(
        Kel103OperatingMode mode,
        string expected)
    {
        Assert.Equal(expected, Kel103OperatingModeMapping.ToNormalizedValue(mode));
    }

    [Fact]
    public void ToNormalizedValue_RejectsUnknownMode()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Kel103OperatingModeMapping.ToNormalizedValue((Kel103OperatingMode)99));
    }
}
