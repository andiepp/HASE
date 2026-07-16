using System.Buffers.Binary;
using Hase.Protocol;

namespace Hase.Protocol.Tests;

public sealed class ProtocolEnvelopeByteCodecTests
{
    private readonly ProtocolEnvelopeByteCodec _codec =
        new();

    [Fact]
    public void Encode_NullEnvelope_ShouldThrow()
    {
        // Act
        void Act()
        {
            _codec.Encode(
                null!);
        }

        // Assert
        ArgumentNullException exception =
            Assert.Throws<ArgumentNullException>(
                Act);

        Assert.Equal(
            "envelope",
            exception.ParamName);
    }

    [Fact]
    public void EncodeDecode_RoundTrip_ShouldPreserveEnvelope()
    {
        // Arrange
        ProtocolEnvelope original =
            CreateEnvelope();

        // Act
        byte[] frame =
            _codec.Encode(
                original);

        ProtocolEnvelope decoded =
            _codec.Decode(
                frame);

        // Assert
        Assert.Equal(
            original.Version,
            decoded.Version);

        Assert.Equal(
            original.Role,
            decoded.Role);

        Assert.Equal(
            original.MessageType,
            decoded.MessageType);

        Assert.Equal(
            original.CorrelationId,
            decoded.CorrelationId);

        Assert.Equal(
            original.PayloadLength,
            decoded.PayloadLength);

        Assert.True(
            original.Payload.Span.SequenceEqual(
                decoded.Payload.Span));
    }

    [Fact]
    public void Encode_ShouldWriteExpectedHeader()
    {
        // Arrange
        ProtocolEnvelope envelope =
            CreateEnvelope();

        // Act
        byte[] frame =
            _codec.Encode(
                envelope);

        // Assert
        Assert.Equal(
            1,
            frame[0]);

        Assert.Equal(
            0,
            frame[1]);

        Assert.Equal(
            (byte)ProtocolMessageRole.Request,
            frame[2]);

        Assert.Equal(
            (byte)ProtocolMessageType
                .ReadEndpointDescriptorRequest,
            frame[3]);

        Assert.Equal(
            envelope.CorrelationId.Value,
            BinaryPrimitives.ReadUInt32LittleEndian(
                frame.AsSpan(
                    4,
                    sizeof(uint))));

        Assert.Equal(
            checked(
                (uint)envelope.PayloadLength),
            BinaryPrimitives.ReadUInt32LittleEndian(
                frame.AsSpan(
                    8,
                    sizeof(uint))));
    }

    [Fact]
    public void Decode_FrameShorterThanHeader_ShouldThrow()
    {
        // Arrange
        byte[] frame =
            new byte[11];

        // Act
        void Act()
        {
            _codec.Decode(
                frame);
        }

        // Assert
        Assert.Throws<InvalidDataException>(
            Act);
    }

    [Fact]
    public void Decode_InvalidPayloadLength_ShouldThrow()
    {
        // Arrange
        byte[] frame =
            new byte[12];

        frame[0] =
            1;

        frame[1] =
            0;

        frame[2] =
            (byte)ProtocolMessageRole.Request;

        frame[3] =
            (byte)ProtocolMessageType
                .ReadEndpointDescriptorRequest;

        BinaryPrimitives.WriteUInt32LittleEndian(
            frame.AsSpan(
                8,
                sizeof(uint)),
            100);

        // Act
        void Act()
        {
            _codec.Decode(
                frame);
        }

        // Assert
        Assert.Throws<InvalidDataException>(
            Act);
    }

    [Fact]
    public void Decode_UnknownRole_ShouldThrow()
    {
        // Arrange
        byte[] frame =
            _codec.Encode(
                CreateEnvelope());

        frame[2] =
            byte.MaxValue;

        // Act
        void Act()
        {
            _codec.Decode(
                frame);
        }

        // Assert
        Assert.Throws<InvalidDataException>(
            Act);
    }

    [Fact]
    public void Decode_UnknownMessageType_ShouldThrow()
    {
        // Arrange
        byte[] frame =
            _codec.Encode(
                CreateEnvelope());

        frame[3] =
            byte.MaxValue;

        // Act
        void Act()
        {
            _codec.Decode(
                frame);
        }

        // Assert
        Assert.Throws<InvalidDataException>(
            Act);
    }

    [Fact]
    public void Decode_ExtraTrailingBytes_ShouldThrow()
    {
        // Arrange
        byte[] frame =
            _codec.Encode(
                CreateEnvelope());

        Array.Resize(
            ref frame,
            frame.Length + 1);

        // Act
        void Act()
        {
            _codec.Decode(
                frame);
        }

        // Assert
        Assert.Throws<InvalidDataException>(
            Act);
    }

    private static ProtocolEnvelope CreateEnvelope()
    {
        return new ProtocolEnvelope(
            new ProtocolVersion(
                1,
                0),
            ProtocolMessageRole.Request,
            ProtocolMessageType
                .ReadEndpointDescriptorRequest,
            new CorrelationId(
                12345),
            new byte[]
            {
                1,
                2,
                3,
                4,
                5
            });
    }
}