using Hase.ProtocolExplorer.Scenarios;
using Xunit;

namespace Hase.ProtocolExplorer.Tests;

public sealed class CapabilityC026ScenarioTests
{
    [Fact]
    public void Name_ShouldBeC026()
    {
        var scenario =
            new CapabilityC026Scenario();

        Assert.Equal(
            "c026",
            scenario.Name);
    }

    [Fact]
    public void ParseArguments_Esp32Host_ReturnsNativeSelection()
    {
        CapabilityC026Arguments arguments =
            CapabilityC026Scenario.ParseArguments(
                [
                    "esp32",
                    "192.168.0.223"
                ]);

        Assert.Equal(
            CapabilityC026EndpointFamily.Esp32,
            arguments.EndpointFamily);

        Assert.Equal(
            "192.168.0.223",
            arguments.Esp32Host);
    }

    [Fact]
    public void ParseArguments_ArduinoDefaults_ReturnsCompactSelection()
    {
        CapabilityC026Arguments arguments =
            CapabilityC026Scenario.ParseArguments(
                [
                    "arduino"
                ]);

        Assert.Equal(
            CapabilityC026EndpointFamily.Arduino,
            arguments.EndpointFamily);

        Assert.Equal(
            115200,
            arguments.BaudRate);

        Assert.Equal(
            TimeSpan.FromSeconds(
                3),
            arguments.VerificationTimeout);
    }

    [Fact]
    public void ParseArguments_ArduinoExplicitValues_ReturnsValues()
    {
        CapabilityC026Arguments arguments =
            CapabilityC026Scenario.ParseArguments(
                [
                    "arduino",
                    "57600",
                    "5"
                ]);

        Assert.Equal(
            57600,
            arguments.BaudRate);

        Assert.Equal(
            TimeSpan.FromSeconds(
                5),
            arguments.VerificationTimeout);
    }

    [Theory]
    [InlineData()]
    [InlineData("unknown")]
    [InlineData("esp32")]
    [InlineData("esp32", "host", "unexpected")]
    [InlineData("arduino", "115200", "3", "unexpected")]
    public void ParseArguments_InvalidShape_Throws(
        params string[] arguments)
    {
        Assert.Throws<ArgumentException>(
            () => CapabilityC026Scenario.ParseArguments(
                arguments));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("invalid")]
    public void ParseArguments_InvalidArduinoBaudRate_Throws(
        string baudRate)
    {
        Assert.Throws<ArgumentException>(
            () => CapabilityC026Scenario.ParseArguments(
                [
                    "arduino",
                    baudRate
                ]));
    }
}
