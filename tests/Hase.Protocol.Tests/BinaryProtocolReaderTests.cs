using Hase.Protocol;

namespace Hase.Protocol.Tests;

public sealed class BinaryProtocolReaderTests
{
    [Fact]
    public void ReadByte_ReturnsByteAndAdvancesPosition()
    {
        BinaryProtocolReader reader =
            new(new byte[] { 0x12, 0x34 });

        byte value =
            reader.ReadByte();

        Assert.Equal(0x12, value);
        Assert.Equal(1, reader.Remaining);
    }

    [Fact]
    public void ReadUInt16_ReadsLittleEndianValue()
    {
        BinaryProtocolReader reader =
            new(new byte[] { 0x34, 0x12 });

        ushort value =
            reader.ReadUInt16();

        Assert.Equal(
            (ushort)0x1234,
            value);

        Assert.Equal(
            0,
            reader.Remaining);
    }

    [Fact]
    public void ReadDouble_ReadsLittleEndianIeee754Value()
    {
        BinaryProtocolReader reader =
            new(new byte[]
            {
                0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0xF0, 0x3F
            });

        double value =
            reader.ReadDouble();

        Assert.Equal(1.0, value);
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void ReadDouble_ThrowsWhenInputIsIncomplete()
    {
        BinaryProtocolReader reader =
            new(new byte[]
            {
                0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0xF0
            });

        Assert.Throws<InvalidDataException>(
            () => reader.ReadDouble());
    }

    [Fact]
    public void ReadCount_ReturnsUInt16ValueAsInt32()
    {
        BinaryProtocolReader reader =
            new(new byte[] { 0x2C, 0x01 });

        int count =
            reader.ReadCount();

        Assert.Equal(300, count);
    }

    [Fact]
    public void ReadString_ReadsUtf8LengthAndBytes()
    {
        BinaryProtocolReader reader =
            new(new byte[]
            {
                0x03, 0x00,
                0x41, 0x42, 0x43
            });

        string value =
            reader.ReadString();

        Assert.Equal("ABC", value);
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void ReadString_ReadsEmptyString()
    {
        BinaryProtocolReader reader =
            new(new byte[] { 0x00, 0x00 });

        string value =
            reader.ReadString();

        Assert.Equal(string.Empty, value);
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void ReadString_ReadsUtf8Characters()
    {
        BinaryProtocolReader reader =
            new(new byte[]
            {
                0x02, 0x00,
                0xC3, 0xA4
            });

        string value =
            reader.ReadString();

        Assert.Equal("ä", value);
    }

    [Fact]
    public void ReadByte_ThrowsWhenNoBytesRemain()
    {
        BinaryProtocolReader reader =
            new(ReadOnlyMemory<byte>.Empty);

        Assert.Throws<InvalidDataException>(
            () => reader.ReadByte());
    }

    [Fact]
    public void ReadUInt16_ThrowsWhenInputIsIncomplete()
    {
        BinaryProtocolReader reader =
            new(new byte[] { 0x34 });

        Assert.Throws<InvalidDataException>(
            () => reader.ReadUInt16());
    }

    [Fact]
    public void ReadString_ThrowsWhenPayloadIsShorterThanDeclaredLength()
    {
        BinaryProtocolReader reader =
            new(new byte[]
            {
                0x03, 0x00,
                0x41, 0x42
            });

        Assert.Throws<InvalidDataException>(
            () => reader.ReadString());
    }

    [Fact]
    public void ReadString_ThrowsForInvalidUtf8()
    {
        BinaryProtocolReader reader =
            new(new byte[]
            {
                0x02, 0x00,
                0xC3, 0x28
            });

        Assert.Throws<InvalidDataException>(
            () => reader.ReadString());
    }

    [Fact]
    public void EnsureFullyConsumed_DoesNothingWhenNoBytesRemain()
    {
        BinaryProtocolReader reader =
            new(new byte[] { 0x01 });

        reader.ReadByte();

        reader.EnsureFullyConsumed();
    }

    [Fact]
    public void EnsureFullyConsumed_ThrowsWhenBytesRemain()
    {
        BinaryProtocolReader reader =
            new(new byte[] { 0x01 });

        Assert.Throws<InvalidDataException>(
            () => reader.EnsureFullyConsumed());
    }
}