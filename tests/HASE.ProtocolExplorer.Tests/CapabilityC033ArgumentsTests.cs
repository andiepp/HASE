using Hase.ProtocolExplorer.Scenarios;
using Xunit;

namespace Hase.ProtocolExplorer.Tests;

public sealed class CapabilityC033ArgumentsTests
{
    [Fact]
    public void Parse_NoArguments_ShouldUseDefaults()
    {
        CapabilityC033Arguments result =
            CapabilityC033Arguments.Parse(
                Array.Empty<string>());

        Assert.Equal(
            115200,
            result.BaudRate);
        Assert.Equal(
            TimeSpan.FromSeconds(
                3),
            result.VerificationTimeout);
    }

    [Fact]
    public void Parse_CustomValues_ShouldPreserveValues()
    {
        CapabilityC033Arguments result =
            CapabilityC033Arguments.Parse(
                [
                    "57600",
                    "5"
                ]);

        Assert.Equal(
            57600,
            result.BaudRate);
        Assert.Equal(
            TimeSpan.FromSeconds(
                5),
            result.VerificationTimeout);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("invalid")]
    public void Parse_InvalidBaudRate_ShouldReject(
        string value)
    {
        Assert.Throws<ArgumentException>(
            () =>
                CapabilityC033Arguments.Parse(
                    [
                        value
                    ]));
    }

    [Fact]
    public void Parse_TooManyArguments_ShouldReject()
    {
        Assert.Throws<ArgumentException>(
            () =>
                CapabilityC033Arguments.Parse(
                    [
                        "115200",
                        "3",
                        "unexpected"
                    ]));
    }
}
