using Hase.ProtocolExplorer.Scenarios;
using Xunit;

namespace Hase.ProtocolExplorer.Tests;

public sealed class CapabilityC028ArgumentsTests
{
    [Fact]
    public void Parse_Esp32Host_ReturnsNativeSelection()
    {
        CapabilityC028Arguments arguments =
            CapabilityC028Arguments.Parse(
                [
                    "esp32",
                    "192.168.0.223"
                ]);

        Assert.Equal(
            CapabilityC028EndpointFamily.Esp32,
            arguments.EndpointFamily);

        Assert.Equal(
            "192.168.0.223",
            arguments.Esp32Host);

        Assert.Equal(
            0,
            arguments.BaudRate);

        Assert.Equal(
            TimeSpan.Zero,
            arguments.VerificationTimeout);
    }

    [Fact]
    public void Parse_EndpointFamily_IsCaseInsensitive()
    {
        CapabilityC028Arguments arguments =
            CapabilityC028Arguments.Parse(
                [
                    "ESP32",
                    "sensor.local"
                ]);

        Assert.Equal(
            CapabilityC028EndpointFamily.Esp32,
            arguments.EndpointFamily);
    }

    [Fact]
    public void Parse_ArduinoDefaults_ReturnsCompactSelection()
    {
        CapabilityC028Arguments arguments =
            CapabilityC028Arguments.Parse(
                [
                    "arduino"
                ]);

        Assert.Equal(
            CapabilityC028EndpointFamily.Arduino,
            arguments.EndpointFamily);

        Assert.Null(
            arguments.Esp32Host);

        Assert.Equal(
            115200,
            arguments.BaudRate);

        Assert.Equal(
            TimeSpan.FromSeconds(
                3),
            arguments.VerificationTimeout);
    }

    [Fact]
    public void Parse_ArduinoExplicitValues_ReturnsValues()
    {
        CapabilityC028Arguments arguments =
            CapabilityC028Arguments.Parse(
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
    public void Parse_InvalidShape_Throws(
        params string[] arguments)
    {
        Assert.Throws<ArgumentException>(
            () => CapabilityC028Arguments.Parse(
                arguments));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("invalid")]
    public void Parse_InvalidArduinoBaudRate_Throws(
        string baudRate)
    {
        Assert.Throws<ArgumentException>(
            () => CapabilityC028Arguments.Parse(
                [
                    "arduino",
                    baudRate
                ]));
    }
}