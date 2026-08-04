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
}
