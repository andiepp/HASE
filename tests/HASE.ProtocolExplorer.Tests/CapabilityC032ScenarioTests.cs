using Hase.ProtocolExplorer.Scenarios;
using Xunit;

namespace Hase.ProtocolExplorer.Tests;

public sealed class CapabilityC032ScenarioTests
{
    [Fact]
    public void Name_ShouldBeC032()
    {
        var scenario =
            new CapabilityC032Scenario();

        Assert.Equal(
            "c032",
            scenario.Name);
    }

    [Fact]
    public void Scenario_ShouldImplementParameterizedScenario()
    {
        var scenario =
            new CapabilityC032Scenario();

        Assert.IsAssignableFrom<IParameterizedScenario>(
            scenario);
    }

    [Fact]
    public void ParseArguments_EndpointHost_ShouldReturnArguments()
    {
        CapabilityC032Arguments arguments =
            CapabilityC032Scenario.ParseArguments(
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
            () => CapabilityC032Scenario.ParseArguments(
                arguments));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void ParseArguments_MissingEndpointHost_ShouldReject(
        string endpointHost)
    {
        Assert.Throws<ArgumentException>(
            () => CapabilityC032Scenario.ParseArguments(
                [
                    endpointHost
                ]));
    }
}
