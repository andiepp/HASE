using Hase.Client;

namespace Hase.Client.Tests;

public sealed class RemoteObservationSequenceTests
{
    [Theory]
    [InlineData(0UL)]
    [InlineData(1UL)]
    [InlineData(18446744073709551615UL)]
    public void Constructor_Value_ShouldPreserveOpaqueSequence(
        ulong value)
    {
        var sequence =
            new RemoteObservationSequence(
                value);

        Assert.Equal(
            value,
            sequence.Value);
        Assert.Equal(
            value.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            sequence.ToString());
    }

    [Fact]
    public void Equality_SameValue_ShouldBeEqual()
    {
        Assert.Equal(
            new RemoteObservationSequence(
                42),
            new RemoteObservationSequence(
                42));
    }
}
