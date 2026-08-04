using Hase.ProtocolExplorer.Scenarios;
using Hase.ProtocolExplorer.ScpiCharacterization;
using Xunit;

namespace Hase.ProtocolExplorer.Tests.Scenarios;

public sealed class Kel103ModeSelectionCharacterizationScenarioTests
{
    [Theory]
    [InlineData("cv", 1)]
    [InlineData("CR", 2)]
    [InlineData("Cw", 3)]
    [InlineData("SHORT", 4)]
    public void ParseDestination_AcceptsOnlyBoundedDestinations(
        string value,
        int expectedValue)
    {
        Assert.Equal(
            (Kel103ModeSelection)expectedValue,
            Kel103ModeSelectionCharacterizationScenario.ParseDestination(value));
    }

    [Theory]
    [InlineData("cc")]
    [InlineData("mode")]
    [InlineData(":FUNCtion VOLT")]
    [InlineData("arbitrary")]
    public void ParseDestination_RejectsOtherValues(string value)
    {
        Assert.Throws<ArgumentException>(() =>
            Kel103ModeSelectionCharacterizationScenario.ParseDestination(value));
    }

    [Fact]
    public void FramingAndBaudValidation_AcceptsOnlyEstablishedValues()
    {
        Kel103ModeSelectionCharacterizationScenario.ValidateTerminator("cr");
        Assert.Equal(
            115200,
            Kel103ModeSelectionCharacterizationScenario.ParseBaudRate("115200"));

        Assert.Throws<ArgumentException>(() =>
            Kel103ModeSelectionCharacterizationScenario.ValidateTerminator("lf"));
        Assert.Throws<ArgumentException>(() =>
            Kel103ModeSelectionCharacterizationScenario.ParseBaudRate("9600"));
    }

    [Fact]
    public void Name_RegistersOnlyTheBoundedModeSelectionScenario()
    {
        var scenario = new Kel103ModeSelectionCharacterizationScenario();

        Assert.Equal("kel103-mode-select-characterize", scenario.Name);
    }
}
