using Hase.ProtocolExplorer.Scenarios;
using Xunit;

namespace HASE.ProtocolExplorer.Tests;

public sealed class CapabilityC023ScenarioTests
{
    [Fact]
    public void Name_ShouldBeC023()
    {
        var scenario =
            new CapabilityC023Scenario();

        Assert.Equal(
            "c023",
            scenario.Name);
    }

    [Fact]
    public void ParseArguments_NoArguments_ShouldUsePhysicalDefaults()
    {
        (int baudRate, TimeSpan timeout) =
            CapabilityC023Scenario.ParseArguments(
                []);

        Assert.Equal(
            115200,
            baudRate);

        Assert.Equal(
            TimeSpan.FromSeconds(
                3),
            timeout);
    }

    [Fact]
    public void ParseArguments_ExplicitValues_ShouldReturnValues()
    {
        (int baudRate, TimeSpan timeout) =
            CapabilityC023Scenario.ParseArguments(
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
            timeout);
    }

    [Fact]
    public void ParseArguments_InvalidBaudRate_ShouldThrow()
    {
        void Act()
        {
            _ = CapabilityC023Scenario.ParseArguments(
                [
                    "invalid"
                ]);
        }

        Assert.Throws<ArgumentException>(
            Act);
    }

    [Fact]
    public void ParseArguments_NonPositiveTimeout_ShouldThrow()
    {
        void Act()
        {
            _ = CapabilityC023Scenario.ParseArguments(
                [
                    "115200",
                    "0"
                ]);
        }

        Assert.Throws<ArgumentException>(
            Act);
    }

    [Fact]
    public void ParseArguments_TooManyArguments_ShouldThrow()
    {
        void Act()
        {
            _ = CapabilityC023Scenario.ParseArguments(
                [
                    "115200",
                    "3",
                    "unexpected"
                ]);
        }

        Assert.Throws<ArgumentException>(
            Act);
    }
}