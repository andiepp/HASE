using Hase.Core.Domain.Data;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Protocol.Serialization;

namespace Hase.Protocol.Tests;

public sealed class ByteArrayPropertyOperationSerializationTests
{
    private static readonly DateTimeOffset TestTimestamp =
        new(
            2026,
            7,
            28,
            15,
            30,
            0,
            123,
            TimeSpan.Zero);

    [Fact]
    public void PropertyValue_RoundTrip_PreservesByteArrayAndMetadata()
    {
        PropertyValueSerializer serializer =
            new();

        PropertyValue original =
            CreatePropertyValue(
                new ByteArrayValue(
                    new byte[]
                    {
                        0x00,
                        0x7F,
                        0x80,
                        0xFF
                    }));

        BinaryProtocolWriter writer =
            new();

        serializer.Write(
            writer,
            original);

        BinaryProtocolReader reader =
            new(
                writer.ToArray());

        PropertyValue decoded =
            serializer.Read(
                reader);

        Assert.Equal(
            original,
            decoded);

        Assert.IsType<ByteArrayValue>(
            decoded.Value);

        Assert.Equal(
            0,
            reader.Remaining);
    }

    [Fact]
    public void PropertyValue_RoundTrip_PreservesEmptyByteArrayDistinctFromNull()
    {
        PropertyValueSerializer serializer =
            new();

        PropertyValue original =
            CreatePropertyValue(
                new ByteArrayValue(
                    Array.Empty<byte>()));

        BinaryProtocolWriter writer =
            new();

        serializer.Write(
            writer,
            original);

        PropertyValue decoded =
            serializer.Read(
                new BinaryProtocolReader(
                    writer.ToArray()));

        ByteArrayValue value =
            Assert.IsType<ByteArrayValue>(
                decoded.Value);

        Assert.Equal(
            0,
            value.Length);

        Assert.NotNull(
            decoded.Value);
    }

    [Fact]
    public void ReadPropertyResponse_RoundTrip_PreservesByteArrayPropertyValue()
    {
        BinaryProtocolPayloadCodec codec =
            new();

        ReadPropertyResponse original =
            new(
                new CorrelationId(
                    3601),
                ProtocolResult.Success,
                CreatePropertyValue(
                    CreateByteArrayValue()));

        ReadPropertyResponse decoded =
            Assert.IsType<ReadPropertyResponse>(
                RoundTrip(
                    codec,
                    original));

        Assert.Equal(
            original,
            decoded);

        Assert.IsType<ByteArrayValue>(
            decoded.PropertyValue!.Value);
    }

    [Fact]
    public void WritePropertyRequest_RoundTrip_PreservesByteArrayValue()
    {
        BinaryProtocolPayloadCodec codec =
            new();

        WritePropertyRequest original =
            new(
                new CorrelationId(
                    3602),
                new InstrumentId(
                    "binary-controller"),
                new PropertyId(
                    "payload"),
                CreateByteArrayValue());

        WritePropertyRequest decoded =
            Assert.IsType<WritePropertyRequest>(
                RoundTrip(
                    codec,
                    original));

        Assert.Equal(
            original,
            decoded);

        Assert.IsType<ByteArrayValue>(
            decoded.Value);
    }

    [Fact]
    public void WritePropertyResponse_RoundTrip_PreservesConfirmedByteArrayValue()
    {
        BinaryProtocolPayloadCodec codec =
            new();

        WritePropertyResponse original =
            new(
                new CorrelationId(
                    3603),
                ProtocolResult.Success,
                CreatePropertyValue(
                    CreateByteArrayValue()));

        WritePropertyResponse decoded =
            Assert.IsType<WritePropertyResponse>(
                RoundTrip(
                    codec,
                    original));

        Assert.Equal(
            original,
            decoded);

        Assert.IsType<ByteArrayValue>(
            decoded.PropertyValue!.Value);
    }

    private static ProtocolMessage RoundTrip(
        BinaryProtocolPayloadCodec codec,
        ProtocolMessage original)
    {
        ProtocolEnvelope envelope =
            codec.Encode(
                original);

        return codec.Decode(
            envelope);
    }

    private static PropertyValue CreatePropertyValue(
        ByteArrayValue value)
    {
        return new PropertyValue(
            value,
            TestTimestamp,
            PropertyQuality.Good);
    }

    private static ByteArrayValue CreateByteArrayValue()
    {
        return new ByteArrayValue(
            new byte[]
            {
                0x00,
                0x01,
                0xFE,
                0xFF
            });
    }
}
