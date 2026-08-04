using Hase.ProtocolExplorer.Scenarios;
using Hase.ProtocolExplorer.ScpiCharacterization;
using Xunit;

namespace Hase.ProtocolExplorer.Tests.Scenarios;

public sealed class Kel103SetpointWriteCharacterizationScenarioTests
{
    [Theory]
    [InlineData("target-voltage", 2)]
    [InlineData("TARGET-CURRENT", 3)]
    [InlineData("Target-Resistance", 4)]
    [InlineData("target-power", 5)]
    public void ParseCandidate_AcceptsOnlyFixedSetpointTargets(
        string value,
        int expectedValue)
    {
        Assert.Equal(
            (Kel103StateCandidate)expectedValue,
            Kel103SetpointWriteCharacterizationScenario.ParseCandidate(value));
    }

    [Theory]
    [InlineData("mode")]
    [InlineData("input-state")]
    [InlineData("voltage")]
    [InlineData(":VOLTage 1V")]
    public void ParseCandidate_RejectsOtherValues(string value)
    {
        Assert.Throws<ArgumentException>(() =>
            Kel103SetpointWriteCharacterizationScenario.ParseCandidate(value));
    }

    [Fact]
    public void FramingAndBaudValidation_AcceptsOnlyEstablishedValues()
    {
        Kel103SetpointWriteCharacterizationScenario.ValidateTerminator("cr");
        Assert.Equal(
            115200,
            Kel103SetpointWriteCharacterizationScenario.ParseBaudRate("115200"));

        Assert.Throws<ArgumentException>(() =>
            Kel103SetpointWriteCharacterizationScenario.ValidateTerminator("lf"));
        Assert.Throws<ArgumentException>(() =>
            Kel103SetpointWriteCharacterizationScenario.ParseBaudRate("9600"));
    }

    [Fact]
    public void Name_RegistersOnlyTheBoundedSetpointScenario()
    {
        var scenario = new Kel103SetpointWriteCharacterizationScenario();

        Assert.Equal("kel103-setpoint-write-characterize", scenario.Name);
    }
}
