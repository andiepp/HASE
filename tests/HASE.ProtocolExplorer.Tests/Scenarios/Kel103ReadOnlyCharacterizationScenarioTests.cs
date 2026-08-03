using Xunit;
using Hase.ProtocolExplorer.Scenarios;
using Hase.ProtocolExplorer.ScpiCharacterization;

namespace Hase.ProtocolExplorer.Tests.Scenarios;

public sealed class Kel103ReadOnlyCharacterizationScenarioTests
{
    [Theory]
    [InlineData(
        "cr",
        0)]
    [InlineData(
        "CR",
        0)]
    [InlineData(
        "lf",
        1)]
    [InlineData(
        "crlf",
        2)]
    public void ParseCommandTerminator_AcceptsExplicitTokens(
        string value,
        int expectedValue)
    {
        var expected =
            (Kel103CommandTerminator)expectedValue;

        Assert.Equal(
            expected,
            Kel103ReadOnlyCharacterizationScenario.ParseCommandTerminator(
                value));
    }

    [Fact]
    public void ParseCommandTerminator_RejectsUnsupportedValue()
    {
        Assert.Throws<ArgumentException>(() =>
            Kel103ReadOnlyCharacterizationScenario.ParseCommandTerminator(
                "automatic"));
    }

    [Theory]
    [InlineData(
        "115200",
        115200)]
    [InlineData(
        "9600",
        9600)]
    public void ParseBaudRate_AcceptsPositiveInvariantInteger(
        string value,
        int expected)
    {
        Assert.Equal(
            expected,
            Kel103ReadOnlyCharacterizationScenario.ParseBaudRate(
                value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("115,200")]
    public void ParseBaudRate_RejectsInvalidValue(
        string value)
    {
        Assert.Throws<ArgumentException>(() =>
            Kel103ReadOnlyCharacterizationScenario.ParseBaudRate(
                value));
    }
}
