using System.Globalization;
using Hase.ProtocolExplorer.ScpiCharacterization;
using Xunit;

namespace Hase.ProtocolExplorer.Tests.ScpiCharacterization;

public sealed class Kel103StateResponseParserTests
{
    [Theory]
    [InlineData("CC")]
    [InlineData("CV")]
    [InlineData("CR")]
    [InlineData("CW")]
    [InlineData("SHORt")]
    public void Parse_AcceptsOnlyPhysicallyEstablishedSteadyStateModeTokens(string response)
    {
        Assert.Equal(
            response,
            Kel103StateResponseParser.Parse(response, Kel103StateCandidate.Mode));
    }

    [Theory]
    [InlineData("OFF", "Off")]
    [InlineData("ON", "On")]
    public void Parse_NormalizesExactInputStateTokens(string response, string expected)
    {
        Assert.Equal(
            expected,
            Kel103StateResponseParser.Parse(response, Kel103StateCandidate.InputState));
    }

    [Theory]
    [InlineData(2, "12.500V", "12.500")]
    [InlineData(3, "+1.250A", "1.250")]
    [InlineData(4, "100.00OHM", "100.00")]
    [InlineData(5, "25.000W", "25.000")]
    public void Parse_AcceptsInvariantSetpointWithExactUnit(
        int candidateValue,
        string response,
        string expected)
    {
        Assert.Equal(
            expected,
            Kel103StateResponseParser.Parse(
                response,
                (Kel103StateCandidate)candidateValue));
    }

    [Theory]
    [InlineData(0, "CURR")]
    [InlineData(0, "VOLT")]
    [InlineData(0, "RES")]
    [InlineData(0, "POW")]
    [InlineData(0, "SHORT")]
    [InlineData(0, "Short")]
    [InlineData(0, "curr")]
    [InlineData(0, "CC ")]
    [InlineData(1, "0")]
    [InlineData(1, "1")]
    [InlineData(1, "off")]
    [InlineData(1, "on")]
    [InlineData(1, "2")]
    [InlineData(2, "12.5")]
    [InlineData(2, "12.5A")]
    [InlineData(2, "12,5V")]
    [InlineData(2, "1e2V")]
    [InlineData(2, "NaNV")]
    [InlineData(2, "InfinityV")]
    [InlineData(2, " 12.5V")]
    [InlineData(2, "12.5V ")]
    [InlineData(4, "100.0Ohm")]
    [InlineData(4, "100.0Ω")]
    [InlineData(5, "25.0W\n")]
    public void Parse_RejectsUnsupportedSyntaxWithoutEchoingResponse(
        int candidateValue,
        string response)
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            Kel103StateResponseParser.Parse(
                response,
                (Kel103StateCandidate)candidateValue));

        Assert.Equal(
            $"The {((Kel103StateCandidate)candidateValue).ToArgumentValue()} response does not match the expected KEL-103 format.",
            exception.Message);
    }

    [Fact]
    public void Parse_IsInvariantUnderNonEnglishCulture()
    {
        CultureInfo original = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            Assert.Equal(
                "12.500",
                Kel103StateResponseParser.Parse(
                    "12.500V",
                    Kel103StateCandidate.TargetVoltage));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Parse_RejectsMissingResponseAndUnsupportedCandidate()
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            Kel103StateResponseParser.Parse(null!, Kel103StateCandidate.Mode));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Kel103StateResponseParser.Parse("value", (Kel103StateCandidate)99));
    }
}
