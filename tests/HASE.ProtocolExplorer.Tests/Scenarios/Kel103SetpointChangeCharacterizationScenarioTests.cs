using Hase.ProtocolExplorer.Scenarios;
using Hase.ProtocolExplorer.ScpiCharacterization;
using Xunit;

namespace Hase.ProtocolExplorer.Tests.Scenarios;

public sealed class Kel103SetpointChangeCharacterizationScenarioTests
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
            Kel103SetpointChangeCharacterizationScenario.ParseCandidate(value));
    }

    [Theory]
    [InlineData("mode")]
    [InlineData("input-state")]
    [InlineData("voltage")]
    [InlineData(":VOLTage 1V")]
    public void ParseCandidate_RejectsOtherValues(string value)
    {
        Assert.Throws<ArgumentException>(() =>
            Kel103SetpointChangeCharacterizationScenario.ParseCandidate(value));
    }

    [Fact]
    public void FramingAndBaudValidation_AcceptsOnlyEstablishedValues()
    {
        Kel103SetpointChangeCharacterizationScenario.ValidateTerminator("cr");
        Assert.Equal(
            115200,
            Kel103SetpointChangeCharacterizationScenario.ParseBaudRate("115200"));

        Assert.Throws<ArgumentException>(() =>
            Kel103SetpointChangeCharacterizationScenario.ValidateTerminator("lf"));
        Assert.Throws<ArgumentException>(() =>
            Kel103SetpointChangeCharacterizationScenario.ParseBaudRate("9600"));
    }

    [Fact]
    public void Name_RegistersOnlyTheBoundedChangedValueScenario()
    {
        var scenario = new Kel103SetpointChangeCharacterizationScenario();

        Assert.Equal("kel103-setpoint-change-characterize", scenario.Name);
    }
}
