using Xunit;
using Hase.ProtocolExplorer.ScpiCharacterization;

namespace Hase.ProtocolExplorer.Tests.ScpiCharacterization;

public sealed class Kel103CharacterizationOptionsTests
{
    [Fact]
    public void Constructor_UsesApprovedDefaults()
    {
        var options =
            new Kel103CharacterizationOptions(
                Kel103CommandTerminator.CarriageReturn);

        Assert.Equal(
            Kel103CommandTerminator.CarriageReturn,
            options.CommandTerminator);

        Assert.Equal(
            TimeSpan.FromSeconds(3),
            options.TotalResponseTimeout);

        Assert.Equal(
            TimeSpan.FromMilliseconds(200),
            options.PostFirstByteIdleInterval);

        Assert.Equal(
            512,
            options.MaximumResponseBytes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_RejectsNonPositiveMaximumResponseBytes(
        int maximumResponseBytes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Kel103CharacterizationOptions(
                Kel103CommandTerminator.CarriageReturn,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromMilliseconds(10),
                maximumResponseBytes));
    }

    [Fact]
    public void Constructor_RejectsIdleIntervalNotShorterThanTotalTimeout()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Kel103CharacterizationOptions(
                Kel103CommandTerminator.CarriageReturn,
                TimeSpan.FromMilliseconds(10),
                TimeSpan.FromMilliseconds(10),
                maximumResponseBytes: 32));
    }

    [Fact]
    public void Constructor_RejectsUndefinedTerminator()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Kel103CharacterizationOptions(
                (Kel103CommandTerminator)999));
    }
}
