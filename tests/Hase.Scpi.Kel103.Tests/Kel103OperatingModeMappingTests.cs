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
}
