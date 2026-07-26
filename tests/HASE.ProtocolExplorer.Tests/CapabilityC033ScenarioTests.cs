using Hase.ProtocolExplorer.Scenarios;
using Xunit;

namespace Hase.ProtocolExplorer.Tests;

public sealed class CapabilityC033ScenarioTests
{
    [Fact]
    public void Name_ShouldBeC033()
    {
        var scenario =
            new CapabilityC033Scenario();

        Assert.Equal(
            "c033",
            scenario.Name);
    }

    [Fact]
    public void Scenario_ShouldImplementParameterizedScenario()
    {
        var scenario =
            new CapabilityC033Scenario();

        Assert.IsAssignableFrom<IParameterizedScenario>(
            scenario);
    }
}
