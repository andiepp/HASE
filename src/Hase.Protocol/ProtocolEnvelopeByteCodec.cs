using System.Buffers.Binary;

namespace Hase.Protocol;

/// <summary>
/// Encodes and decodes complete protocol envelopes for transport.
/// </summary>
/// <remarks>
/// Frame layout:
///
/// Byte 0      Protocol major version
/// Byte 1      Protocol minor version
/// Byte 2      Protocol message role
/// Byte 3      Protocol message type
/// Bytes 4-7   Correlation identifier, little-endian
/// Bytes 8-11  Payload length, little-endian
/// Bytes 12-n  Payload
/// </remarks>
public sealed class ProtocolEnvelopeByteCodec
{
    private const int HeaderLength =
        12;

    /// <summary>
    /// Encodes a protocol envelope into its transport-frame representation.
    /// </summary>
    public byte[] Encode(
        ProtocolEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(
            envelope);

        int frameLength =
            checked(
                HeaderLength
                + envelope.PayloadLength);

        byte[] frame =
            new byte[frameLength];

        Span<byte> frameSpan =
            frame.AsSpan();

        frameSpan[0] =
            envelope.Version.Major;

        frameSpan[1] =
            envelope.Version.Minor;

        frameSpan[2] =
            (byte)envelope.Role;

        frameSpan[3] =
            (byte)envelope.MessageType;

        BinaryPrimitives.WriteUInt32LittleEndian(
            frameSpan.Slice(
                4,
                sizeof(uint)),
            envelope.CorrelationId.Value);

        BinaryPrimitives.WriteUInt32LittleEndian(
            frameSpan.Slice(
                8,
                sizeof(uint)),
            checked(
                (uint)envelope.PayloadLength));

        envelope.Payload.Span.CopyTo(
            frameSpan.Slice(
                HeaderLength));

        return frame;
    }

    /// <summary>
    /// Decodes a complete transport frame into a protocol envelope.
    /// </summary>
    public ProtocolEnvelope Decode(
        ReadOnlyMemory<byte> frame)
    {
        if (frame.Length < HeaderLength)
        {
            throw new InvalidDataException(
                $"A protocol frame must contain at least "
                + $"{HeaderLength} bytes, but only "
                + $"{frame.Length} bytes were supplied.");
        }

        ReadOnlySpan<byte> frameSpan =
            frame.Span;

        ProtocolVersion version =
            new(
                frameSpan[0],
                frameSpan[1]);

        byte encodedRole =
            frameSpan[2];

        ProtocolMessageRole role =
            (ProtocolMessageRole)encodedRole;

        if (!Enum.IsDefined(
                role))
        {
            throw new InvalidDataException(
                $"Unknown protocol message role "
                + $"'{encodedRole}'.");
        }

        byte encodedMessageType =
            frameSpan[3];

        ProtocolMessageType messageType =
            (ProtocolMessageType)encodedMessageType;

        if (!Enum.IsDefined(
                messageType))
        {
            throw new InvalidDataException(
                $"Unknown protocol message type "
                + $"'{encodedMessageType}'.");
        }

        uint correlationIdValue =
            BinaryPrimitives.ReadUInt32LittleEndian(
                frameSpan.Slice(
                    4,
                    sizeof(uint)));

        uint encodedPayloadLength =
            BinaryPrimitives.ReadUInt32LittleEndian(
                frameSpan.Slice(
                    8,
                    sizeof(uint)));

        if (encodedPayloadLength > int.MaxValue)
        {
            throw new InvalidDataException(
                $"The encoded payload length "
                + $"'{encodedPayloadLength}' exceeds the "
                + "supported maximum.");
        }

        int payloadLength =
            (int)encodedPayloadLength;

        int expectedFrameLength;

        try
        {
            expectedFrameLength =
                checked(
                    HeaderLength
                    + payloadLength);
        }
        catch (OverflowException exception)
        {
            throw new InvalidDataException(
                "The encoded protocol frame length is invalid.",
                exception);
        }

        if (frame.Length
            != expectedFrameLength)
        {
            throw new InvalidDataException(
                $"The protocol frame contains "
                + $"{frame.Length} bytes, but the header declares "
                + $"{payloadLength} payload bytes and therefore "
                + $"requires {expectedFrameLength} frame bytes.");
        }

        ReadOnlyMemory<byte> payload =
            frame.Slice(
                HeaderLength,
                payloadLength);

        return new ProtocolEnvelope(
            version,
            role,
            messageType,
            new CorrelationId(
                correlationIdValue),
            payload);
    }
}