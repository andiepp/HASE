using Hase.ProtocolExplorer.Scenarios;
using Xunit;

namespace Hase.ProtocolExplorer.Tests;

public sealed class CapabilityC027ScenarioTests
{
    [Fact]
    public void Name_ShouldBeC027()
    {
        var scenario =
            new CapabilityC027Scenario();

        Assert.Equal(
            "c027",
            scenario.Name);
    }
}