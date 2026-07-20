using Hase.CompactProtocol;

namespace Hase.CompactProtocol.Tests;

public sealed class CompactSerialFrameCodecTests
{
    [Fact]
    public void Encode_KnownFrame_ShouldMatchGoldenBytes()
    {
        var frame =
            new CompactSerialFrame(
                messageType: 0x02,
                correlationId: 0x03,
                payload:
                [
                    0x10,
                    0x20,
                    0x30
                ]);

        byte[] expected =
        [
            0x48,
            0x53,
            0x01,
            0x02,
            0x03,
            0x03,
            0x10,
            0x20,
            0x30,
            0xC4,
            0x37
        ];

        byte[] actual =
            CompactSerialFrameCodec.Encode(
                frame);

        Assert.Equal(
            expected,
            actual);
    }

    [Fact]
    public void Decode_GoldenBytes_ShouldRestoreFrame()
    {
        byte[] encoded =
        [
            0x48,
            0x53,
            0x01,
            0x02,
            0x03,
            0x03,
            0x10,
            0x20,
            0x30,
            0xC4,
            0x37
        ];

        CompactSerialFrame frame =
            CompactSerialFrameCodec.Decode(
                encoded);

        Assert.Equal(
            0x02,
            frame.MessageType);

        Assert.Equal(
            0x03,
            frame.CorrelationId);

        Assert.Equal(
            new byte[]
            {
                0x10,
                0x20,
                0x30
            },
            frame.Payload.ToArray());
    }

    [Fact]
    public void Encode_EmptyPayload_ShouldProduceMinimumFrame()
    {
        var frame =
            new CompactSerialFrame(
                messageType: 0x01,
                correlationId: 0x01,
                payload: []);

        byte[] encoded =
            CompactSerialFrameCodec.Encode(
                frame);

        Assert.Equal(
            CompactSerialFrameConstants.FrameOverheadLength,
            encoded.Length);

        CompactSerialFrame decoded =
            CompactSerialFrameCodec.Decode(
                encoded);

        Assert.Empty(
            decoded.Payload.ToArray());
    }

    [Fact]
    public void Encode_MaximumPayload_ShouldRoundTrip()
    {
        byte[] payload =
            Enumerable.Range(
                    0,
                    CompactSerialFrameConstants.MaximumPayloadLength)
                .Select(
                    value => (byte)value)
                .ToArray();

        var frame =
            new CompactSerialFrame(
                messageType: 0x7F,
                correlationId: 0xFF,
                payload);

        byte[] encoded =
            CompactSerialFrameCodec.Encode(
                frame);

        Assert.Equal(
            CompactSerialFrameConstants.MaximumFrameLength,
            encoded.Length);

        CompactSerialFrame decoded =
            CompactSerialFrameCodec.Decode(
                encoded);

        Assert.Equal(
            payload,
            decoded.Payload.ToArray());
    }

    [Fact]
    public void Constructor_Payload_ShouldBeCopied()
    {
        byte[] payload =
        [
            0x10,
            0x20
        ];

        var frame =
            new CompactSerialFrame(
                messageType: 0x01,
                correlationId: 0x01,
                payload);

        payload[0] =
            0xFF;

        Assert.Equal(
            new byte[]
            {
                0x10,
                0x20
            },
            frame.Payload.ToArray());
    }

    [Fact]
    public void Constructor_OversizedPayload_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactSerialFrame(
                messageType: 0x01,
                correlationId: 0x01,
                new byte[
                    CompactSerialFrameConstants.MaximumPayloadLength
                    + 1]);
        }

        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                Act);

        Assert.Equal(
            "payload",
            exception.ParamName);
    }

    [Fact]
    public void Encode_NullFrame_ShouldThrow()
    {
        void Act()
        {
            _ = CompactSerialFrameCodec.Encode(
                null!);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Decode_TruncatedFrame_ShouldThrow()
    {
        byte[] encoded =
        [
            0x48,
            0x53,
            0x01,
            0x02,
            0x03,
            0x03,
            0x10,
            0x20,
            0x30,
            0xC4
        ];

        void Act()
        {
            _ = CompactSerialFrameCodec.Decode(
                encoded);
        }

        Assert.Throws<InvalidDataException>(
            Act);
    }

    [Fact]
    public void Decode_TrailingByte_ShouldThrow()
    {
        byte[] encoded =
        [
            0x48,
            0x53,
            0x01,
            0x02,
            0x03,
            0x03,
            0x10,
            0x20,
            0x30,
            0xC4,
            0x37,
            0x00
        ];

        void Act()
        {
            _ = CompactSerialFrameCodec.Decode(
                encoded);
        }

        Assert.Throws<InvalidDataException>(
            Act);
    }

    [Fact]
    public void Decode_InvalidStartMarker_ShouldThrow()
    {
        byte[] encoded =
            CreateValidEncodedFrame();

        encoded[0] =
            0x00;

        void Act()
        {
            _ = CompactSerialFrameCodec.Decode(
                encoded);
        }

        Assert.Throws<InvalidDataException>(
            Act);
    }

    [Fact]
    public void Decode_UnsupportedVersion_ShouldThrow()
    {
        byte[] encoded =
            CreateValidEncodedFrame();

        encoded[2] =
            0x02;

        void Act()
        {
            _ = CompactSerialFrameCodec.Decode(
                encoded);
        }

        Assert.Throws<InvalidDataException>(
            Act);
    }

    [Fact]
    public void Decode_CorruptedPayload_ShouldThrow()
    {
        byte[] encoded =
            CreateValidEncodedFrame();

        encoded[6] ^=
            0xFF;

        void Act()
        {
            _ = CompactSerialFrameCodec.Decode(
                encoded);
        }

        Assert.Throws<InvalidDataException>(
            Act);
    }

    [Fact]
    public void Decode_CorruptedCrc_ShouldThrow()
    {
        byte[] encoded =
            CreateValidEncodedFrame();

        encoded[^1] ^=
            0xFF;

        void Act()
        {
            _ = CompactSerialFrameCodec.Decode(
                encoded);
        }

        Assert.Throws<InvalidDataException>(
            Act);
    }

    private static byte[] CreateValidEncodedFrame()
    {
        return CompactSerialFrameCodec.Encode(
            new CompactSerialFrame(
                messageType: 0x02,
                correlationId: 0x03,
                payload:
                [
                    0x10,
                    0x20,
                    0x30
                ]));
    }
}