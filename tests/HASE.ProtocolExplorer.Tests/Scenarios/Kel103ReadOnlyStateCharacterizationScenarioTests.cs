using Hase.ProtocolExplorer.Scenarios;
using Hase.ProtocolExplorer.ScpiCharacterization;
using Xunit;

namespace Hase.ProtocolExplorer.Tests.Scenarios;

public sealed class Kel103ReadOnlyStateCharacterizationScenarioTests
{
    [Theory]
    [InlineData("mode", 0)]
    [InlineData("INPUT-STATE", 1)]
    [InlineData("Target-Voltage", 2)]
    [InlineData("target-current", 3)]
    [InlineData("TARGET-RESISTANCE", 4)]
    [InlineData("target-power", 5)]
    public void ParseCandidate_AcceptsOnlyCompiledInSelectors(
        string value,
        int expectedValue)
    {
        Assert.Equal(
            (Kel103StateCandidate)expectedValue,
            Kel103ReadOnlyStateCharacterizationScenario.ParseCandidate(value));
    }

    [Theory]
    [InlineData("voltage")]
    [InlineData("input")]
    [InlineData("activate")]
    [InlineData("arbitrary-query")]
    public void ParseCandidate_RejectsOtherSelectors(string value)
    {
        Assert.Throws<ArgumentException>(() =>
            Kel103ReadOnlyStateCharacterizationScenario.ParseCandidate(value));
    }

    [Theory]
    [InlineData("cr")]
    [InlineData("CR")]
    public void ValidateTerminator_AcceptsOnlyCharacterizedCr(string value)
    {
        Kel103ReadOnlyStateCharacterizationScenario.ValidateTerminator(value);
    }

    [Theory]
    [InlineData("lf")]
    [InlineData("crlf")]
    [InlineData("")]
    public void ValidateTerminator_RejectsOtherValues(string value)
    {
        Assert.Throws<ArgumentException>(() =>
            Kel103ReadOnlyStateCharacterizationScenario.ValidateTerminator(value));
    }

    [Fact]
    public void ParseBaudRate_AcceptsOnlyCharacterizedRate()
    {
        Assert.Equal(
            115200,
            Kel103ReadOnlyStateCharacterizationScenario.ParseBaudRate("115200"));
        Assert.Throws<ArgumentException>(() =>
            Kel103ReadOnlyStateCharacterizationScenario.ParseBaudRate("9600"));
    }

    [Fact]
    public void Name_RegistersOnlyTheBoundedStateScenario()
    {
        var scenario = new Kel103ReadOnlyStateCharacterizationScenario();

        Assert.Equal("kel103-state-characterize", scenario.Name);
    }
}
