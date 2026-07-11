using Hase.Protocol;

namespace Hase.Protocol.Tests;

public sealed class BinaryProtocolWriterTests
{
    [Fact]
    public void WriteUInt16_WritesLittleEndianBytes()
    {
        BinaryProtocolWriter writer = new();

        writer.WriteUInt16(0x1234);

        Assert.Equal(
            new byte[] { 0x34, 0x12 },
            writer.ToArray());
    }

    [Fact]
    public void WriteString_WritesUtf8LengthAndBytes()
    {
        BinaryProtocolWriter writer = new();

        writer.WriteString("ABC");

        Assert.Equal(
            new byte[]
            {
                0x03, 0x00,
                0x41, 0x42, 0x43
            },
            writer.ToArray());
    }

    [Fact]
    public void WriteString_WritesEmptyString()
    {
        BinaryProtocolWriter writer = new();

        writer.WriteString(string.Empty);

        Assert.Equal(
            new byte[] { 0x00, 0x00 },
            writer.ToArray());
    }

    [Fact]
    public void WriteString_UsesUtf8ByteLength()
    {
        BinaryProtocolWriter writer = new();

        writer.WriteString("ä");

        Assert.Equal(
            new byte[]
            {
                0x02, 0x00,
                0xC3, 0xA4
            },
            writer.ToArray());
    }

    [Fact]
    public void WriteString_ThrowsForStringLongerThanUInt16ByteLength()
    {
        BinaryProtocolWriter writer = new();

        string value = new('A', ushort.MaxValue + 1);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => writer.WriteString(value));
    }

    [Fact]
    public void WriteCount_WritesUInt16Count()
    {
        BinaryProtocolWriter writer = new();

        writer.WriteCount(300);

        Assert.Equal(
            new byte[] { 0x2C, 0x01 },
            writer.ToArray());
    }

    [Fact]
    public void WriteCount_ThrowsForNegativeCount()
    {
        BinaryProtocolWriter writer = new();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => writer.WriteCount(-1));
    }

    [Fact]
    public void WriteCount_ThrowsForCountLargerThanUInt16()
    {
        BinaryProtocolWriter writer = new();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => writer.WriteCount(ushort.MaxValue + 1));
    }
}