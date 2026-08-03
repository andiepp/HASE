namespace Hase.Scpi.Tests;

public sealed class ScpiTextRequestFormatterTests
{
    [Theory]
    [InlineData(ScpiCommandTerminator.CarriageReturn, "*IDN?\r")]
    [InlineData(ScpiCommandTerminator.LineFeed, "*IDN?\n")]
    [InlineData(ScpiCommandTerminator.CarriageReturnLineFeed, "*IDN?\r\n")]
    public void Format_AppendsConfiguredTerminator(ScpiCommandTerminator terminator, string expected)
    {
        var formatter = CreateFormatter(terminator);

        Assert.Equal(System.Text.Encoding.ASCII.GetBytes(expected), formatter.Format("*IDN?"));
    }

    [Fact]
    public void Format_PreservesPrintableTextExactly()
    {
        var formatter = CreateFormatter();

        Assert.Equal(" VOLT 1.250 \n", System.Text.Encoding.ASCII.GetString(formatter.Format(" VOLT 1.250 ")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Format_RejectsMissingRequest(string? request)
    {
        var formatter = CreateFormatter();

        Assert.ThrowsAny<ArgumentException>(() => formatter.Format(request!));
    }

    [Theory]
    [InlineData("MEAS?\rNEXT?")]
    [InlineData("MEAS?\nNEXT?")]
    [InlineData("MEAS?\t")]
    [InlineData("MEAS?é")]
    public void Format_RejectsNonPrintableOrNonAsciiText(string request)
    {
        var formatter = CreateFormatter();

        Assert.Throws<ArgumentException>(() => formatter.Format(request));
    }

    [Fact]
    public void Format_ReturnsIndependentBuffers()
    {
        var formatter = CreateFormatter();
        var first = formatter.Format("A?");
        var second = formatter.Format("A?");

        Assert.NotSame(first, second);
        Assert.Equal(first, second);
    }

    private static ScpiTextRequestFormatter CreateFormatter(
        ScpiCommandTerminator terminator = ScpiCommandTerminator.LineFeed) =>
        new(new ScpiTextFramingOptions(
            terminator,
            ScpiResponseTerminator.LineFeed,
            TimeSpan.FromSeconds(3),
            512));
}
