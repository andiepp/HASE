using Hase.ProtocolExplorer.Scenarios;
using Xunit;

namespace Hase.ProtocolExplorer.Tests.Scenarios;

public sealed class Kel103InputControlCharacterizationScenarioTests
{
    [Theory]
    [InlineData("true")]
    [InlineData("TRUE")]
    [InlineData("True")]
    public void ParseConfirmation_AcceptsOnlyExplicitTrue(string value)
    {
        Assert.True(Kel103InputControlCharacterizationScenario.ParseConfirmation(value));
    }

    [Theory]
    [InlineData("false")]
    [InlineData("yes")]
    [InlineData("1")]
    public void ParseConfirmation_RejectsMissingOrFalseConfirmation(string value)
    {
        Assert.Throws<ArgumentException>(() =>
            Kel103InputControlCharacterizationScenario.ParseConfirmation(value));
    }

    [Fact]
    public void FramingBaudAndName_AreBounded()
    {
        var scenario = new Kel103InputControlCharacterizationScenario();

        Assert.Equal("kel103-input-control-characterize", scenario.Name);
        Kel103InputControlCharacterizationScenario.ValidateTerminator("cr");
        Assert.Equal(115200,
            Kel103InputControlCharacterizationScenario.ParseBaudRate("115200"));
        Assert.Throws<ArgumentException>(() =>
            Kel103InputControlCharacterizationScenario.ValidateTerminator("lf"));
        Assert.Throws<ArgumentException>(() =>
            Kel103InputControlCharacterizationScenario.ParseBaudRate("9600"));
    }
}
