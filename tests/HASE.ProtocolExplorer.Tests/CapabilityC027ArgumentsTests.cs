using Hase.ProtocolExplorer.Scenarios;
using Xunit;

namespace Hase.ProtocolExplorer.Tests;

public sealed class CapabilityC027ArgumentsTests
{
    [Fact]
    public void Parse_Esp32Host_ReturnsNativeSelection()
    {
        CapabilityC027Arguments arguments =
            CapabilityC027Arguments.Parse(
                [
                    "esp32",
                    "192.168.0.223"
                ]);

        Assert.Equal(
            CapabilityC027EndpointFamily.Esp32,
            arguments.EndpointFamily);

        Assert.Equal(
            "192.168.0.223",
            arguments.Esp32Host);
    }

    [Fact]
    public void Parse_ArduinoDefaults_ReturnsCompactSelection()
    {
        CapabilityC027Arguments arguments =
            CapabilityC027Arguments.Parse(
                [
                    "arduino"
                ]);

        Assert.Equal(
            CapabilityC027EndpointFamily.Arduino,
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
    public void Parse_ArduinoExplicitValues_ReturnsValues()
    {
        CapabilityC027Arguments arguments =
            CapabilityC027Arguments.Parse(
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
            () => CapabilityC027Arguments.Parse(
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
            () => CapabilityC027Arguments.Parse(
                [
                    "arduino",
                    baudRate
                ]));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("invalid")]
    public void Parse_InvalidArduinoVerificationTimeout_Throws(
        string verificationTimeout)
    {
        Assert.Throws<ArgumentException>(
            () => CapabilityC027Arguments.Parse(
                [
                    "arduino",
                    "115200",
                    verificationTimeout
                ]));
    }
}