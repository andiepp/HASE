using Hase.ProtocolExplorer.Scenarios;
using Xunit;

namespace Hase.ProtocolExplorer.Tests;

public sealed class CapabilityC024ScenarioTests
{
    [Fact]
    public void Name_ShouldBeC024()
    {
        var scenario =
            new CapabilityC024Scenario();

        Assert.Equal(
            "c024",
            scenario.Name);
    }

    [Fact]
    public void ParseArguments_NoArguments_ShouldUseDefaults()
    {
        (int baudRate, TimeSpan verificationTimeout) =
            CapabilityC024Scenario.ParseArguments(
                []);

        Assert.Equal(
            115200,
            baudRate);

        Assert.Equal(
            TimeSpan.FromSeconds(
                3),
            verificationTimeout);
    }

    [Fact]
    public void ParseArguments_ExplicitValues_ShouldReturnValues()
    {
        (int baudRate, TimeSpan verificationTimeout) =
            CapabilityC024Scenario.ParseArguments(
                [
                    "57600",
                    "5"
                ]);

        Assert.Equal(
            57600,
            baudRate);

        Assert.Equal(
            TimeSpan.FromSeconds(
                5),
            verificationTimeout);
    }

    [Fact]
    public void ParseArguments_TooManyArguments_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            () => CapabilityC024Scenario.ParseArguments(
                [
                    "115200",
                    "3",
                    "unexpected"
                ]));
    }

    [Fact]
    public void ParseArguments_NonPositiveValue_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            () => CapabilityC024Scenario.ParseArguments(
                [
                    "0"
                ]));
    }
}