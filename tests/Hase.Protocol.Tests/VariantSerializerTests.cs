using Hase.Protocol;

namespace Hase.Protocol.Tests;

public sealed class VariantSerializerTests
{
    [Fact]
    public void Write_Null_WritesNullType()
    {
        VariantSerializer serializer = new();
        BinaryProtocolWriter writer = new();

        serializer.Write(writer, null);

        Assert.Equal(
            new byte[]
            {
                0x00
            },
            writer.ToArray());
    }

    [Fact]
    public void Write_BooleanFalse_WritesExpectedBytes()
    {
        VariantSerializer serializer = new();
        BinaryProtocolWriter writer = new();

        serializer.Write(writer, false);

        Assert.Equal(
            new byte[]
            {
                0x01,
                0x00
            },
            writer.ToArray());
    }

    [Fact]
    public void Write_BooleanTrue_WritesExpectedBytes()
    {
        VariantSerializer serializer = new();
        BinaryProtocolWriter writer = new();

        serializer.Write(writer, true);

        Assert.Equal(
            new byte[]
            {
                0x01,
                0x01
            },
            writer.ToArray());
    }

    [Fact]
    public void Write_Int32_WritesLittleEndianBytes()
    {
        VariantSerializer serializer = new();
        BinaryProtocolWriter writer = new();

        serializer.Write(
            writer,
            0x12345678);

        Assert.Equal(
            new byte[]
            {
                0x02,
                0x78, 0x56, 0x34, 0x12
            },
            writer.ToArray());
    }

    [Fact]
    public void Write_Int64_WritesLittleEndianBytes()
    {
        VariantSerializer serializer = new();
        BinaryProtocolWriter writer = new();

        serializer.Write(
            writer,
            0x0102030405060708L);

        Assert.Equal(
            new byte[]
            {
                0x03,
                0x08, 0x07, 0x06, 0x05,
                0x04, 0x03, 0x02, 0x01
            },
            writer.ToArray());
    }

    [Fact]
    public void Write_Double_WritesExpectedBytes()
    {
        VariantSerializer serializer = new();
        BinaryProtocolWriter writer = new();

        serializer.Write(
            writer,
            1.0);

        Assert.Equal(
            new byte[]
            {
                0x04,
                0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0xF0, 0x3F
            },
            writer.ToArray());
    }

    [Fact]
    public void Write_String_WritesExpectedBytes()
    {
        VariantSerializer serializer = new();
        BinaryProtocolWriter writer = new();

        serializer.Write(
            writer,
            "ABC");

        Assert.Equal(
            new byte[]
            {
                0x05,
                0x03, 0x00,
                (byte)'A', (byte)'B', (byte)'C'
            },
            writer.ToArray());
    }

    [Fact]
    public void Write_EmptyString_WritesZeroLengthString()
    {
        VariantSerializer serializer = new();
        BinaryProtocolWriter writer = new();

        serializer.Write(
            writer,
            string.Empty);

        Assert.Equal(
            new byte[]
            {
                0x05,
                0x00, 0x00
            },
            writer.ToArray());
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    [InlineData(true)]
    [InlineData(-123)]
    [InlineData(1234567890123L)]
    [InlineData(23.5)]
    [InlineData("")]
    [InlineData("Environment")]
    public void RoundTrip_PreservesSupportedValue(
        object? original)
    {
        VariantSerializer serializer = new();
        BinaryProtocolWriter writer = new();

        serializer.Write(
            writer,
            original);

        BinaryProtocolReader reader =
            new(writer.ToArray());

        object? decoded =
            serializer.Read(reader);

        Assert.Equal(
            original,
            decoded);

        Assert.Equal(
            0,
            reader.Remaining);
    }

    [Fact]
    public void Write_UnsupportedType_ThrowsNotSupportedException()
    {
        VariantSerializer serializer = new();
        BinaryProtocolWriter writer = new();

        Assert.Throws<NotSupportedException>(
            () => serializer.Write(
                writer,
                new Version(1, 0)));
    }

    [Fact]
    public void Read_UnknownVariantType_ThrowsInvalidDataException()
    {
        VariantSerializer serializer = new();

        BinaryProtocolReader reader =
            new(new byte[] { 0xFF });

        Assert.Throws<InvalidDataException>(
            () => serializer.Read(reader));
    }

    [Fact]
    public void Read_InvalidBooleanValue_ThrowsInvalidDataException()
    {
        VariantSerializer serializer = new();

        BinaryProtocolReader reader =
            new(new byte[]
            {
                0x01,
                0x02
            });

        Assert.Throws<InvalidDataException>(
            () => serializer.Read(reader));
    }

    [Fact]
    public void Read_TruncatedInt32_ThrowsInvalidDataException()
    {
        VariantSerializer serializer = new();

        BinaryProtocolReader reader =
            new(new byte[]
            {
                0x02,
                0x01, 0x02, 0x03
            });

        Assert.Throws<InvalidDataException>(
            () => serializer.Read(reader));
    }

    [Fact]
    public void Read_TruncatedInt64_ThrowsInvalidDataException()
    {
        VariantSerializer serializer = new();

        BinaryProtocolReader reader =
            new(new byte[]
            {
                0x03,
                0x01, 0x02, 0x03, 0x04
            });

        Assert.Throws<InvalidDataException>(
            () => serializer.Read(reader));
    }
}