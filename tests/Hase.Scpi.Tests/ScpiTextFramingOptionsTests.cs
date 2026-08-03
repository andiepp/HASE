namespace Hase.Scpi.Tests;

public sealed class ScpiTextFramingOptionsTests
{
    [Fact]
    public void Constructor_PreservesExplicitValues()
    {
        var options = new ScpiTextFramingOptions(
            ScpiCommandTerminator.CarriageReturn,
            ScpiResponseTerminator.LineFeed,
            TimeSpan.FromSeconds(3),
            512);

        Assert.Equal(ScpiCommandTerminator.CarriageReturn, options.CommandTerminator);
        Assert.Equal(ScpiResponseTerminator.LineFeed, options.ResponseTerminator);
        Assert.Equal(TimeSpan.FromSeconds(3), options.TotalExchangeTimeout);
        Assert.Equal(512, options.MaximumResponseBytes);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void Constructor_RejectsUndefinedCommandTerminator(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(commandTerminator: (ScpiCommandTerminator)value));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void Constructor_RejectsUndefinedResponseTerminator(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(responseTerminator: (ScpiResponseTerminator)value));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void Constructor_RejectsNonPositiveTimeout(int milliseconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(timeout: TimeSpan.FromMilliseconds(milliseconds)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void Constructor_RejectsNonPositiveMaximumResponseBytes(int maximumResponseBytes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(maximumResponseBytes: maximumResponseBytes));
    }

    private static ScpiTextFramingOptions Create(
        ScpiCommandTerminator commandTerminator = ScpiCommandTerminator.LineFeed,
        ScpiResponseTerminator responseTerminator = ScpiResponseTerminator.LineFeed,
        TimeSpan? timeout = null,
        int maximumResponseBytes = 512) =>
        new(commandTerminator, responseTerminator, timeout ?? TimeSpan.FromSeconds(3), maximumResponseBytes);
}
