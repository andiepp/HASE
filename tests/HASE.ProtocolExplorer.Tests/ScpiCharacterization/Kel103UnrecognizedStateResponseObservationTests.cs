using Hase.ProtocolExplorer.ScpiCharacterization;
using Xunit;

namespace Hase.ProtocolExplorer.Tests.ScpiCharacterization;

public sealed class Kel103UnrecognizedStateResponseObservationTests
{
    [Theory]
    [InlineData(0, "CC")]
    [InlineData(1, "OFF")]
    public void Create_ReportsBoundedPrintableStateToken(
        int candidateValue,
        string response)
    {
        Kel103UnrecognizedStateResponseObservation observation =
            Kel103UnrecognizedStateResponseObservation.Create(
                response,
                (Kel103StateCandidate)candidateValue);

        Assert.Equal(response.Length, observation.ResponseCharacterCount);
        Assert.Equal(response, observation.ObservedToken);
        Assert.False(observation.ContainsWhitespace);
        Assert.False(observation.ContainsUnexpectedCharacters);
    }

    [Theory]
    [InlineData("CC ")]
    [InlineData("C\tC")]
    public void Create_DoesNotPrintUnsafeStateToken(string response)
    {
        Kel103UnrecognizedStateResponseObservation observation =
            Kel103UnrecognizedStateResponseObservation.Create(
                response,
                Kel103StateCandidate.Mode);

        Assert.Null(observation.ObservedToken);
        Assert.True(observation.ContainsWhitespace);
        Assert.True(observation.ContainsUnexpectedCharacters);
    }

    [Theory]
    [InlineData("12.345", false, 2, ".", 3, "None", false)]
    [InlineData("+12.345VDC", true, 2, ".", 3, "VDC", false)]
    [InlineData("100,50OHM", false, 3, ",", 2, "OHM", false)]
    [InlineData("25W", false, 2, "None", 0, "W", false)]
    [InlineData(" 25W", false, 0, "None", 0, "None", true)]
    public void Create_ReportsSetpointShapeWithoutNumericValue(
        string response,
        bool hasLeadingSign,
        int integerDigits,
        string decimalSeparator,
        int fractionalDigits,
        string suffix,
        bool unexpected)
    {
        Kel103UnrecognizedStateResponseObservation observation =
            Kel103UnrecognizedStateResponseObservation.Create(
                response,
                Kel103StateCandidate.TargetVoltage);

        Assert.Equal(response.Length, observation.ResponseCharacterCount);
        Assert.Null(observation.ObservedToken);
        Assert.Equal(hasLeadingSign, observation.HasLeadingSign);
        Assert.Equal(integerDigits, observation.IntegerDigitCount);
        Assert.Equal(decimalSeparator, observation.DecimalSeparator);
        Assert.Equal(fractionalDigits, observation.FractionalDigitCount);
        Assert.Equal(suffix, observation.Suffix);
        Assert.Equal(unexpected, observation.ContainsUnexpectedCharacters);
        Assert.DoesNotContain("12.345", observation.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Create_RejectsMissingResponse()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Kel103UnrecognizedStateResponseObservation.Create(
                null!,
                Kel103StateCandidate.Mode));
    }
}
