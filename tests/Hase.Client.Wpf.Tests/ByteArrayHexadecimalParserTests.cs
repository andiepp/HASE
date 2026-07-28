using Hase.Client.Wpf.Services;
using Hase.Core.Domain.Data;

namespace Hase.Client.Wpf.Tests;

public sealed class ByteArrayHexadecimalParserTests
{
    [Theory]
    [InlineData("00", 0x00)]
    [InlineData("7f", 0x7F)]
    [InlineData("FF", 0xFF)]
    public void TryParse_OneByte_ShouldAcceptCaseInsensitiveHexadecimal(
        string text,
        int expected)
    {
        Assert.True(
            ByteArrayHexadecimalParser.TryParse(
                text,
                out ByteArrayValue? result));
        Assert.NotNull(
            result);
        Assert.Equal(
            checked(
                (byte)expected),
            Assert.Single(
                result.ToArray()));
    }

    [Fact]
    public void TryParse_WhitespaceSeparatedBytes_ShouldPreserveExactBytes()
    {
        Assert.True(
            ByteArrayHexadecimalParser.TryParse(
                "00 7f\r\nFF",
                out ByteArrayValue? result));

        Assert.Equal(
            new byte[]
            {
                0x00,
                0x7F,
                0xFF
            },
            result!.ToArray());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("0")]
    [InlineData("000")]
    [InlineData("GG")]
    [InlineData("0x00")]
    [InlineData("00-FF")]
    public void TryParse_InvalidInput_ShouldReturnFalse(
        string? text)
    {
        Assert.False(
            ByteArrayHexadecimalParser.TryParse(
                text,
                out ByteArrayValue? result));
        Assert.Null(
            result);
    }
}
