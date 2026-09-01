using Hase.ProtocolExplorer.ScpiCharacterization;
using Xunit;

namespace Hase.ProtocolExplorer.Tests.ScpiCharacterization;

public sealed class Kel103SetpointChangeCandidateTests
{
    [Fact]
    public void Create_PrefersOneIncreasingResponseScaleQuantum()
    {
        Kel103SetpointChangeCandidate candidate = Kel103SetpointChangeCandidate.Create(
            "0.1000",
            "0.1000",
            "120.00");

        Assert.Equal("0.1000", candidate.OriginalValue);
        Assert.Equal("0.1001", candidate.ChangedValue);
    }

    [Fact]
    public void Create_DecreasesWhenIncreaseWouldReachUpperBound()
    {
        Kel103SetpointChangeCandidate candidate = Kel103SetpointChangeCandidate.Create(
            "10.0",
            "0.0",
            "10.0");

        Assert.Equal("9.9", candidate.ChangedValue);
    }

    [Theory]
    [InlineData("0", "0", "1")]
    [InlineData("-1", "0", "10")]
    [InlineData("5", "10", "0")]
    public void Create_RejectsWhenNoSafeInteriorCandidateExists(
        string original,
        string lower,
        string upper)
    {
        Assert.Throws<InvalidDataException>(() =>
            Kel103SetpointChangeCandidate.Create(original, lower, upper));
    }

    [Theory]
    [InlineData("ORIGINAL-SECRET", "0", "10", "ORIGINAL-SECRET")]
    [InlineData("1", "LOWER-SECRET", "10", "LOWER-SECRET")]
    [InlineData("1", "0", "UPPER-SECRET", "UPPER-SECRET")]
    public void Create_RejectsNonInvariantInputsWithoutDisclosingThem(
        string original,
        string lower,
        string upper,
        string secret)
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            Kel103SetpointChangeCandidate.Create(original, lower, upper));

        Assert.DoesNotContain(secret, exception.Message, StringComparison.Ordinal);
    }
}
