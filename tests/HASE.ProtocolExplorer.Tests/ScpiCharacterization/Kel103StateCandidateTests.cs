using Hase.ProtocolExplorer.ScpiCharacterization;
using Xunit;

namespace Hase.ProtocolExplorer.Tests.ScpiCharacterization;

public sealed class Kel103StateCandidateTests
{
    [Theory]
    [InlineData(0, "mode", ":FUNCtion?", null)]
    [InlineData(1, "input-state", ":INPut?", null)]
    [InlineData(2, "target-voltage", ":VOLTage?", "V")]
    [InlineData(3, "target-current", ":CURRent?", "A")]
    [InlineData(4, "target-resistance", ":RESistance?", "OHM")]
    [InlineData(5, "target-power", ":POWer?", "W")]
    public void Candidate_MapsToOneFixedArgumentQueryAndUnit(
        int candidateValue,
        string argument,
        string query,
        string? unit)
    {
        var candidate = (Kel103StateCandidate)candidateValue;

        Assert.Equal(argument, candidate.ToArgumentValue());
        Assert.Equal(query, candidate.ToQueryText());
        Assert.Equal(unit, candidate.ToUnitSymbol());
    }

    [Fact]
    public void Candidate_RejectsUnsupportedValue()
    {
        var candidate = (Kel103StateCandidate)99;

        Assert.Throws<ArgumentOutOfRangeException>(() => candidate.ToArgumentValue());
        Assert.Throws<ArgumentOutOfRangeException>(() => candidate.ToQueryText());
        Assert.Throws<ArgumentOutOfRangeException>(() => candidate.ToUnitSymbol());
    }

    [Theory]
    [InlineData(2, "1.25", ":VOLTage 1.25V")]
    [InlineData(3, "2.5", ":CURRent 2.5A")]
    [InlineData(4, "3.75", ":RESistance 3.75OHM")]
    [InlineData(5, "4.5", ":POWer 4.5W")]
    public void Candidate_MapsSetpointToOneFixedSetter(
        int candidateValue,
        string normalizedValue,
        string expectedSetter)
    {
        Assert.Equal(
            expectedSetter,
            ((Kel103StateCandidate)candidateValue).ToSetterText(normalizedValue));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(99)]
    public void Candidate_RejectsSetterForNonSetpoint(int candidateValue)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ((Kel103StateCandidate)candidateValue).ToSetterText("1"));
    }

    [Theory]
    [InlineData("1,25")]
    [InlineData("1E2")]
    [InlineData("1V")]
    [InlineData("1;:INPut ON")]
    public void Candidate_RejectsNonDecimalSetterValue(string value)
    {
        Assert.Throws<ArgumentException>(() =>
            Kel103StateCandidate.TargetVoltage.ToSetterText(value));
    }
}
