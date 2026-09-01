using Hase.ProtocolExplorer.ScpiCharacterization;
using Xunit;

namespace Hase.ProtocolExplorer.Tests.ScpiCharacterization;

public sealed class Kel103MeasurementCandidateTests
{
    [Theory]
    [InlineData(0, "voltage", ":MEASure:VOLTage?", "V")]
    [InlineData(1, "current", ":MEASure:CURRent?", "A")]
    [InlineData(2, "power", ":MEASure:POWer?", "W")]
    public void Candidate_HasExactCompiledInMapping(
        int candidateValue,
        string argument,
        string query,
        string unit)
    {
        var candidate = (Kel103MeasurementCandidate)candidateValue;

        Assert.Equal(argument, candidate.ToArgumentValue());
        Assert.Equal(query, candidate.ToQueryText());
        Assert.Equal(unit, candidate.ToUnitSymbol());
    }

    [Fact]
    public void Candidate_RejectsUndefinedValues()
    {
        var candidate = (Kel103MeasurementCandidate)99;

        Assert.Throws<ArgumentOutOfRangeException>(() => candidate.ToArgumentValue());
        Assert.Throws<ArgumentOutOfRangeException>(() => candidate.ToQueryText());
        Assert.Throws<ArgumentOutOfRangeException>(() => candidate.ToUnitSymbol());
    }
}
