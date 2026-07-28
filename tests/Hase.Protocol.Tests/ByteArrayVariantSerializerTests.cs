using Hase.Core.Domain.Data;
using Hase.Protocol;

namespace Hase.Protocol.Tests;

public sealed class ByteArrayVariantSerializerTests
{
    [Fact]
    public void Write_ByteArray_WritesExpectedBytes()
    {
        VariantSerializer serializer = new();
        BinaryProtocolWriter writer = new();

        serializer.Write(
            writer,
            new ByteArrayValue(
                new byte[]
                {
                    0x00,
                    0x7F,
                    0x80,
                    0xFF
                }));

        Assert.Equal(
            new byte[]
            {
                0x06,
                0x04, 0x00,
                0x00, 0x7F, 0x80, 0xFF
            },
            writer.ToArray());
    }

    [Fact]
    public void RoundTrip_EmptyByteArray_PreservesPresentEmptyValue()
    {
        ByteArrayValue original =
            new(Array.Empty<byte>());

        ByteArrayValue decoded =
            RoundTrip(original);

        Assert.Equal(
            original,
            decoded);

        Assert.Equal(
            0,
            decoded.Length);
    }

    [Fact]
    public void RoundTrip_ByteArray_PreservesEveryByte()
    {
        ByteArrayValue original =
            new(
                Enumerable.Range(
                    0,
                    256)
                    .Select(value => (byte)value)
                    .ToArray());

        ByteArrayValue decoded =
            RoundTrip(original);

        Assert.Equal(
            original,
            decoded);
    }

    [Fact]
    public void Write_MaximumLengthByteArray_Succeeds()
    {
        VariantSerializer serializer = new();
        BinaryProtocolWriter writer = new();

        serializer.Write(
            writer,
            new ByteArrayValue(
                new byte[ushort.MaxValue]));

        byte[] encoded =
            writer.ToArray();

        Assert.Equal(
            ushort.MaxValue + 3,
            encoded.Length);

        Assert.Equal(
            0x06,
            encoded[0]);

        Assert.Equal(
            0xFF,
            encoded[1]);

        Assert.Equal(
            0xFF,
            encoded[2]);
    }

    [Fact]
    public void Write_ByteArrayExceedingMaximumLength_ThrowsArgumentOutOfRangeException()
    {
        VariantSerializer serializer = new();
        BinaryProtocolWriter writer = new();

        ByteArrayValue value =
            new(new byte[ushort.MaxValue + 1]);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => serializer.Write(
                writer,
                value));
    }

    [Fact]
    public void Write_RawByteArray_RemainsUnsupported()
    {
        VariantSerializer serializer = new();
        BinaryProtocolWriter writer = new();

        Assert.Throws<NotSupportedException>(
            () => serializer.Write(
                writer,
                new byte[]
                {
                    0x01
                }));
    }

    [Fact]
    public void Read_TruncatedByteArray_ThrowsInvalidDataException()
    {
        VariantSerializer serializer = new();

        BinaryProtocolReader reader =
            new(new byte[]
            {
                0x06,
                0x03, 0x00,
                0x01, 0x02
            });

        Assert.Throws<InvalidDataException>(
            () => serializer.Read(reader));
    }

    private static ByteArrayValue RoundTrip(
        ByteArrayValue original)
    {
        VariantSerializer serializer = new();
        BinaryProtocolWriter writer = new();

        serializer.Write(
            writer,
            original);

        BinaryProtocolReader reader =
            new(writer.ToArray());

        ByteArrayValue decoded =
            Assert.IsType<ByteArrayValue>(
                serializer.Read(reader));

        Assert.Equal(
            0,
            reader.Remaining);

        return decoded;
    }
}
