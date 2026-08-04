namespace Hase.Scpi.Kel103.Tests;

public sealed class Kel103InputStateMappingTests
{
    [Fact]
    public void Mapping_HasExactPropertyAndQueryMetadata()
    {
        Assert.Equal("input-enabled", Kel103InputStateMapping.PropertyId.Value);
        Assert.Equal("Input.Enabled", Kel103InputStateMapping.PropertyPath.ToString());
        Assert.Equal(":INPut?", Kel103InputStateMapping.Query);
    }

    [Theory]
    [InlineData("OFF", false)]
    [InlineData("ON", true)]
    public void ParseResponse_RecognizesExactCharacterizedTokens(
        string response,
        bool expected)
    {
        Assert.Equal(expected, Kel103InputStateMapping.ParseResponse(response));
    }

    [Theory]
    [InlineData("")]
    [InlineData("off")]
    [InlineData("on")]
    [InlineData("OFF ")]
    [InlineData(" ON")]
    [InlineData("1")]
    public void ParseResponse_RejectsUnsupportedSyntax(string response)
    {
        Assert.Throws<InvalidDataException>(() =>
            Kel103InputStateMapping.ParseResponse(response));
    }

    [Fact]
    public void ParseResponse_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Kel103InputStateMapping.ParseResponse(null!));
    }
}
