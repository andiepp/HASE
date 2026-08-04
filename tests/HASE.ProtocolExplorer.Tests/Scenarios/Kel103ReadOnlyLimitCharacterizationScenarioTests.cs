using Hase.ProtocolExplorer.Scenarios;
using Hase.ProtocolExplorer.ScpiCharacterization;
using Xunit;

namespace Hase.ProtocolExplorer.Tests.Scenarios;

public sealed class Kel103ReadOnlyLimitCharacterizationScenarioTests
{
    [Theory]
    [InlineData("target-voltage", 2)]
    [InlineData("TARGET-CURRENT", 3)]
    [InlineData("Target-Resistance", 4)]
    [InlineData("target-power", 5)]
    public void ParseCandidate_AcceptsOnlyCompiledInTargets(
        string value,
        int expectedValue)
    {
        Assert.Equal(
            (Kel103StateCandidate)expectedValue,
            Kel103ReadOnlyLimitCharacterizationScenario.ParseCandidate(value));
    }

    [Theory]
    [InlineData("mode")]
    [InlineData("input-state")]
    [InlineData("voltage")]
    [InlineData("arbitrary-query")]
    public void ParseCandidate_RejectsOtherSelectors(string value)
    {
        Assert.Throws<ArgumentException>(() =>
            Kel103ReadOnlyLimitCharacterizationScenario.ParseCandidate(value));
    }

    [Theory]
    [InlineData("lower", 0)]
    [InlineData("LOWER", 0)]
    [InlineData("Upper", 1)]
    public void ParseLimit_AcceptsOnlyCompiledInSelectors(
        string value,
        int expectedValue)
    {
        Assert.Equal(
            (Kel103SetpointLimit)expectedValue,
            Kel103ReadOnlyLimitCharacterizationScenario.ParseLimit(value));
    }

    [Theory]
    [InlineData("minimum")]
    [InlineData("maximum")]
    [InlineData("MIN")]
    [InlineData("MAX")]
    [InlineData("0")]
    [InlineData("100")]
    [InlineData(":VOLT:UPP?")]
    public void ParseLimit_RejectsQueryAndSetterShapedValues(string value)
    {
        Assert.Throws<ArgumentException>(() =>
            Kel103ReadOnlyLimitCharacterizationScenario.ParseLimit(value));
    }

    [Theory]
    [InlineData("cr")]
    [InlineData("CR")]
    public void ValidateTerminator_AcceptsOnlyCharacterizedCr(string value)
    {
        Kel103ReadOnlyLimitCharacterizationScenario.ValidateTerminator(value);
    }

    [Theory]
    [InlineData("lf")]
    [InlineData("crlf")]
    [InlineData("")]
    public void ValidateTerminator_RejectsOtherValues(string value)
    {
        Assert.Throws<ArgumentException>(() =>
            Kel103ReadOnlyLimitCharacterizationScenario.ValidateTerminator(value));
    }

    [Fact]
    public void ParseBaudRate_AcceptsOnlyCharacterizedRate()
    {
        Assert.Equal(
            115200,
            Kel103ReadOnlyLimitCharacterizationScenario.ParseBaudRate("115200"));
        Assert.Throws<ArgumentException>(() =>
            Kel103ReadOnlyLimitCharacterizationScenario.ParseBaudRate("9600"));
    }

    [Fact]
    public void Name_RegistersOnlyTheBoundedLimitScenario()
    {
        var scenario = new Kel103ReadOnlyLimitCharacterizationScenario();

        Assert.Equal("kel103-limit-characterize", scenario.Name);
    }
}
