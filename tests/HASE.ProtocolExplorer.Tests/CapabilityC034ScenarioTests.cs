using Hase.ProtocolExplorer.Scenarios;
using Xunit;

namespace Hase.ProtocolExplorer.Tests;

public sealed class CapabilityC034ScenarioTests
{
    [Fact]
    public void Name_ShouldBeC034()
    {
        Assert.Equal(
            "c034",
            new CapabilityC034Scenario().Name);
    }

    [Fact]
    public void Scenario_ShouldImplementParameterizedScenario()
    {
        Assert.IsAssignableFrom<IParameterizedScenario>(
            new CapabilityC034Scenario());
    }

    [Fact]
    public void ParseArguments_EndpointHost_ShouldPreserveValue()
    {
        CapabilityC034Arguments arguments =
            CapabilityC034Scenario.ParseArguments(
                [
                    "192.168.0.223"
                ]);

        Assert.Equal(
            "192.168.0.223",
            arguments.EndpointHost);
    }

    [Theory]
    [InlineData()]
    [InlineData("host", "unexpected")]
    public void ParseArguments_InvalidShape_ShouldReject(
        params string[] arguments)
    {
        Assert.Throws<ArgumentException>(
            () => CapabilityC034Scenario.ParseArguments(
                arguments));
    }
}
