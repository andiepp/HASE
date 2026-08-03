using System.Text;

namespace Hase.Scpi.Tests;

public sealed class ScpiTextResponseFramerTests
{
    [Theory]
    [InlineData(ScpiResponseTerminator.CarriageReturn, "VALUE\r")]
    [InlineData(ScpiResponseTerminator.LineFeed, "VALUE\n")]
    [InlineData(ScpiResponseTerminator.CarriageReturnLineFeed, "VALUE\r\n")]
    public void Append_CompletesAtConfiguredTerminator(ScpiResponseTerminator terminator, string bytes)
    {
        var framer = CreateFramer(terminator);

        framer.Append(Encoding.ASCII.GetBytes(bytes));

        Assert.True(framer.IsComplete);
        Assert.Equal("VALUE", framer.Complete());
    }

    [Fact]
    public void Append_RecognizesTerminatorAcrossChunkBoundary()
    {
        var framer = CreateFramer(ScpiResponseTerminator.CarriageReturnLineFeed);

        framer.Append("VALUE\r"u8);
        Assert.False(framer.IsComplete);
        framer.Append("\n"u8);

        Assert.Equal("VALUE", framer.Complete());
    }

    [Fact]
    public void Append_AllowsEmptyPayload()
    {
        var framer = CreateFramer();

        framer.Append("\n"u8);

        Assert.Equal(string.Empty, framer.Complete());
    }

    [Fact]
    public void Append_AllowsResponseExactlyAtMaximumIncludingTerminator()
    {
        var framer = CreateFramer(maximumResponseBytes: 4);

        framer.Append("ABC\n"u8);

        Assert.Equal("ABC", framer.Complete());
    }

    [Fact]
    public void Append_RejectsResponseBeyondMaximumIncludingTerminator()
    {
        var framer = CreateFramer(maximumResponseBytes: 3);

        Assert.Throws<InvalidDataException>(() => framer.Append("ABC\n"u8));
        Assert.Throws<InvalidOperationException>(() => framer.Complete());
    }

    [Fact]
    public void Append_RejectsNonPrintableAsciiPayload()
    {
        var framer = CreateFramer();

        Assert.Throws<InvalidDataException>(() => framer.Append([0x41, 0x09, 0x0A]));
    }

    [Fact]
    public void Append_RejectsNonAsciiPayload()
    {
        var framer = CreateFramer();

        Assert.Throws<InvalidDataException>(() => framer.Append([0x80, 0x0A]));
    }

    [Fact]
    public void Append_RejectsInvalidCarriageReturnLineFeedSequence()
    {
        var framer = CreateFramer(ScpiResponseTerminator.CarriageReturnLineFeed);

        Assert.Throws<InvalidDataException>(() => framer.Append("VALUE\rX"u8));
    }

    [Fact]
    public void Append_RejectsBytesAfterTerminatorInSameChunk()
    {
        var framer = CreateFramer();

        Assert.Throws<InvalidDataException>(() => framer.Append("VALUE\nMORE"u8));
    }

    [Fact]
    public void Append_RejectsBytesAfterTerminatorInLaterChunk()
    {
        var framer = CreateFramer();
        framer.Append("VALUE\n"u8);

        Assert.Throws<InvalidDataException>(() => framer.Append("MORE"u8));
    }

    [Fact]
    public void Complete_RejectsPrematureCompletion()
    {
        var framer = CreateFramer();
        framer.Append("VALUE"u8);

        Assert.Throws<InvalidOperationException>(() => framer.Complete());
    }

    [Fact]
    public void Complete_RejectsSecondCompletion()
    {
        var framer = CreateFramer();
        framer.Append("VALUE\n"u8);
        Assert.Equal("VALUE", framer.Complete());

        Assert.Throws<InvalidOperationException>(() => framer.Complete());
    }

    private static ScpiTextResponseFramer CreateFramer(
        ScpiResponseTerminator terminator = ScpiResponseTerminator.LineFeed,
        int maximumResponseBytes = 512) =>
        new(new ScpiTextFramingOptions(
            ScpiCommandTerminator.LineFeed,
            terminator,
            TimeSpan.FromSeconds(3),
            maximumResponseBytes));
}
