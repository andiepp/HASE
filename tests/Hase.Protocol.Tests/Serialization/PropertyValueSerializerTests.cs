using Hase.Core.Domain.Properties;
using Hase.Protocol.Serialization;

namespace Hase.Protocol.Tests.Serialization;

public sealed class PropertyValueSerializerTests
{
    private static readonly DateTimeOffset TestTimestamp =
        new(
            2026,
            7,
            12,
            10,
            15,
            30,
            123,
            TimeSpan.Zero);

    [Fact]
    public void Write_NullValue_WritesExpectedBytes()
    {
        PropertyValueSerializer serializer = new();
        BinaryProtocolWriter writer = new();

        PropertyValue propertyValue = new(
            null,
            DateTimeOffset.UnixEpoch,
            PropertyQuality.Good);

        serializer.Write(
            writer,
            propertyValue);

        Assert.Equal(
            new byte[]
            {
                // VariantType.Null
                0x00,

                // Unix time: 0 milliseconds
                0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00,

                // PropertyQuality.Good
                0x00
            },
            writer.ToArray());
    }

    [Fact]
    public void Read_NullValue_ReturnsExpectedPropertyValue()
    {
        PropertyValueSerializer serializer = new();

        BinaryProtocolReader reader = new(
            new byte[]
            {
                0x00,

                0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00,

                0x00
            });

        PropertyValue propertyValue =
            serializer.Read(reader);

        Assert.Null(
            propertyValue.Value);

        Assert.Equal(
            DateTimeOffset.UnixEpoch,
            propertyValue.TimestampUtc);

        Assert.Equal(
            PropertyQuality.Good,
            propertyValue.Quality);

        Assert.Equal(
            0,
            reader.Remaining);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    [InlineData(true)]
    [InlineData(-42)]
    [InlineData(1234567890123L)]
    [InlineData(23.5)]
    [InlineData("")]
    [InlineData("Environment")]
    public void RoundTrip_PreservesSupportedValue(
        object? value)
    {
        PropertyValueSerializer serializer = new();

        PropertyValue original = new(
            value,
            TestTimestamp,
            PropertyQuality.Good);

        BinaryProtocolWriter writer = new();

        serializer.Write(
            writer,
            original);

        BinaryProtocolReader reader =
            new(writer.ToArray());

        PropertyValue decoded =
            serializer.Read(reader);

        Assert.Equal(
            original.Value,
            decoded.Value);

        Assert.Equal(
            original.TimestampUtc,
            decoded.TimestampUtc);

        Assert.Equal(
            original.Quality,
            decoded.Quality);

        Assert.Equal(
            0,
            reader.Remaining);
    }

    [Theory]
    [InlineData(PropertyQuality.Good)]
    [InlineData(PropertyQuality.Uncertain)]
    [InlineData(PropertyQuality.Bad)]
    public void RoundTrip_PreservesQuality(
        PropertyQuality quality)
    {
        PropertyValueSerializer serializer = new();

        PropertyValue original = new(
            17,
            TestTimestamp,
            quality);

        BinaryProtocolWriter writer = new();

        serializer.Write(
            writer,
            original);

        BinaryProtocolReader reader =
            new(writer.ToArray());

        PropertyValue decoded =
            serializer.Read(reader);

        Assert.Equal(
            quality,
            decoded.Quality);
    }

    [Fact]
    public void RoundTrip_PreservesUnixEpoch()
    {
        PropertyValueSerializer serializer = new();

        PropertyValue original = new(
            1,
            DateTimeOffset.UnixEpoch,
            PropertyQuality.Good);

        BinaryProtocolWriter writer = new();

        serializer.Write(
            writer,
            original);

        BinaryProtocolReader reader =
            new(writer.ToArray());

        PropertyValue decoded =
            serializer.Read(reader);

        Assert.Equal(
            DateTimeOffset.UnixEpoch,
            decoded.TimestampUtc);
    }

    [Fact]
    public void RoundTrip_PreservesTimestampBeforeUnixEpoch()
    {
        PropertyValueSerializer serializer = new();

        DateTimeOffset timestamp =
            DateTimeOffset.UnixEpoch.AddMilliseconds(-1234);

        PropertyValue original = new(
            1,
            timestamp,
            PropertyQuality.Good);

        BinaryProtocolWriter writer = new();

        serializer.Write(
            writer,
            original);

        BinaryProtocolReader reader =
            new(writer.ToArray());

        PropertyValue decoded =
            serializer.Read(reader);

        Assert.Equal(
            timestamp,
            decoded.TimestampUtc);
    }

    [Fact]
    public void Write_UnsupportedValueType_ThrowsNotSupportedException()
    {
        PropertyValueSerializer serializer = new();
        BinaryProtocolWriter writer = new();

        PropertyValue propertyValue = new(
            new Version(1, 0),
            TestTimestamp,
            PropertyQuality.Good);

        Assert.Throws<NotSupportedException>(
            () => serializer.Write(
                writer,
                propertyValue));
    }

    [Fact]
    public void Read_UnknownQuality_ThrowsInvalidDataException()
    {
        PropertyValueSerializer serializer = new();
        BinaryProtocolWriter writer = new();

        new VariantSerializer().Write(
            writer,
            17);

        WriteInt64(
            writer,
            TestTimestamp.ToUnixTimeMilliseconds());

        writer.WriteByte(0xFF);

        BinaryProtocolReader reader =
            new(writer.ToArray());

        Assert.Throws<InvalidDataException>(
            () => serializer.Read(reader));
    }

    [Fact]
    public void Read_TruncatedTimestamp_ThrowsInvalidDataException()
    {
        PropertyValueSerializer serializer = new();

        BinaryProtocolReader reader = new(
            new byte[]
            {
                // VariantType.Null
                0x00,

                // Only seven of eight timestamp bytes
                0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00
            });

        Assert.Throws<InvalidDataException>(
            () => serializer.Read(reader));
    }

    [Fact]
    public void Read_MissingQuality_ThrowsInvalidDataException()
    {
        PropertyValueSerializer serializer = new();
        BinaryProtocolWriter writer = new();

        new VariantSerializer().Write(
            writer,
            null);

        WriteInt64(
            writer,
            TestTimestamp.ToUnixTimeMilliseconds());

        BinaryProtocolReader reader =
            new(writer.ToArray());

        Assert.Throws<InvalidDataException>(
            () => serializer.Read(reader));
    }

    private static void WriteInt64(
        BinaryProtocolWriter writer,
        long value)
    {
        Span<byte> bytes =
            stackalloc byte[sizeof(long)];

        System.Buffers.Binary.BinaryPrimitives
            .WriteInt64LittleEndian(
                bytes,
                value);

        foreach (byte item in bytes)
        {
            writer.WriteByte(item);
        }
    }
}
