using System.Globalization;
using Hase.ProtocolExplorer.ScpiCharacterization;
using Xunit;

namespace Hase.ProtocolExplorer.Tests.ScpiCharacterization;

public sealed class Kel103MeasurementResponseParserTests
{
    [Theory]
    [InlineData(0, "1.4999V", "1.4999")]
    [InlineData(1, "0.789A", "0.789")]
    [InlineData(2, "1.1968W", "1.1968")]
    [InlineData(0, "-0.001V", "-0.001")]
    [InlineData(1, "+0.000A", "0.000")]
    public void Parse_AcceptsInvariantNumberWithExactUnit(
        int candidateValue,
        string response,
        string expected)
    {
        var candidate = (Kel103MeasurementCandidate)candidateValue;
        decimal value = Kel103MeasurementResponseParser.Parse(response, candidate);

        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), value);
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("1.0A")]
    [InlineData("1,0V")]
    [InlineData("1e2V")]
    [InlineData("NaNV")]
    [InlineData("InfinityV")]
    [InlineData(" 1.0V")]
    [InlineData("1.0V ")]
    [InlineData("1.0VV")]
    [InlineData("1.0V\n")]
    [InlineData("VALUEV")]
    public void Parse_RejectsMalformedVoltageWithoutEchoingResponse(string response)
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            Kel103MeasurementResponseParser.Parse(
                response,
                Kel103MeasurementCandidate.Voltage));

        Assert.DoesNotContain(response, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_RejectsMissingResponse()
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            Kel103MeasurementResponseParser.Parse(
                null!,
                Kel103MeasurementCandidate.Voltage));
    }

    [Fact]
    public void Parse_IsInvariantUnderNonEnglishCulture()
    {
        CultureInfo original = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            Assert.Equal(
                1.25m,
                Kel103MeasurementResponseParser.Parse(
                    "1.25V",
                    Kel103MeasurementCandidate.Voltage));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
