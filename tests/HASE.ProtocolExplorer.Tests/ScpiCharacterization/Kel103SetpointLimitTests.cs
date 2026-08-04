using Hase.ProtocolExplorer.ScpiCharacterization;
using Xunit;

namespace Hase.ProtocolExplorer.Tests.ScpiCharacterization;

public sealed class Kel103SetpointLimitTests
{
    [Theory]
    [InlineData(0, "lower")]
    [InlineData(1, "upper")]
    public void Limit_MapsToFixedArgument(
        int limitValue,
        string argument)
    {
        var limit = (Kel103SetpointLimit)limitValue;

        Assert.Equal(argument, limit.ToArgumentValue());
    }

    [Theory]
    [InlineData(2, 0, ":VOLT:LOW?")]
    [InlineData(2, 1, ":VOLT:UPP?")]
    [InlineData(3, 0, ":CURR:LOW?")]
    [InlineData(3, 1, ":CURR:UPP?")]
    [InlineData(4, 0, ":RES:LOW?")]
    [InlineData(4, 1, ":RES:UPP?")]
    [InlineData(5, 0, ":POW:LOW?")]
    [InlineData(5, 1, ":POW:UPP?")]
    public void Limit_MapsToOneFixedReadOnlyQuery(
        int candidateValue,
        int limitValue,
        string expected)
    {
        Assert.Equal(
            expected,
            ((Kel103SetpointLimit)limitValue).ToQueryText(
                (Kel103StateCandidate)candidateValue));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Limit_RejectsNonSetpointCandidate(int candidateValue)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Kel103SetpointLimit.Lower.ToQueryText(
                (Kel103StateCandidate)candidateValue));
    }

    [Fact]
    public void Limit_RejectsUnsupportedValue()
    {
        var limit = (Kel103SetpointLimit)99;

        Assert.Throws<ArgumentOutOfRangeException>(() => limit.ToArgumentValue());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            limit.ToQueryText(Kel103StateCandidate.TargetVoltage));
    }
}
