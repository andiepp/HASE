using Hase.ProtocolExplorer.Scenarios;
using Xunit;

namespace Hase.ProtocolExplorer.Tests;

public sealed class CapabilityC025ScenarioTests
{
    [Fact]
    public void Name_ShouldBeC025()
    {
        var scenario =
            new CapabilityC025Scenario();

        Assert.Equal(
            "c025",
            scenario.Name);
    }

    [Fact]
    public void ParseArguments_NoArguments_ShouldUseDefaults()
    {
        (int baudRate, TimeSpan verificationTimeout) =
            CapabilityC025Scenario.ParseArguments(
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
            CapabilityC025Scenario.ParseArguments(
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
    public void ParseArguments_InvalidArguments_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(
            () => CapabilityC025Scenario.ParseArguments(
                [
                    "115200",
                    "3",
                    "unexpected"
                ]));

        Assert.Throws<ArgumentException>(
            () => CapabilityC025Scenario.ParseArguments(
                [
                    "0"
                ]));
    }
}