using Hase.ProtocolExplorer.Scenarios;
using Xunit;

namespace Hase.ProtocolExplorer.Tests;

public sealed class CapabilityC028ScenarioTests
{
    [Fact]
    public void Name_ShouldBeC028()
    {
        var scenario =
            new CapabilityC028Scenario();

        Assert.Equal(
            "c028",
            scenario.Name);
    }
}