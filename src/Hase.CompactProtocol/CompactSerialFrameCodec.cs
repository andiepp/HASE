using System.Buffers.Binary;

namespace Hase.CompactProtocol;

/// <summary>
/// Encodes and decodes complete Compact Serial Protocol Version 1 frames.
/// </summary>
internal static class CompactSerialFrameCodec
{
    public static byte[] Encode(
        CompactSerialFrame frame)
    {
        ArgumentNullException.ThrowIfNull(
            frame);

        ReadOnlySpan<byte> payload =
            frame.Payload.Span;

        var encoded =
            new byte[
                CompactSerialFrameConstants.FrameOverheadLength
                + payload.Length];

        encoded[0] =
            CompactSerialFrameConstants.StartMarkerFirstByte;

        encoded[1] =
            CompactSerialFrameConstants.StartMarkerSecondByte;

        encoded[2] =
            CompactSerialFrameConstants.ProtocolVersion;

        encoded[3] =
            frame.MessageType;

        encoded[4] =
            frame.CorrelationId;

        encoded[5] =
            checked((byte)payload.Length);

        payload.CopyTo(
            encoded.AsSpan(
                6,
                payload.Length));

        ushort crc =
            Crc16CcittFalse.Calculate(
                encoded.AsSpan(
                    2,
                    4 + payload.Length));

        BinaryPrimitives.WriteUInt16BigEndian(
            encoded.AsSpan(
                6 + payload.Length,
                sizeof(ushort)),
            crc);

        return encoded;
    }

    public static CompactSerialFrame Decode(
        ReadOnlySpan<byte> encoded)
    {
        if (encoded.Length
            < CompactSerialFrameConstants.FrameOverheadLength)
        {
            throw new InvalidDataException(
                "The compact serial frame is shorter than the minimum "
                + "frame length.");
        }

        if (encoded[0]
                != CompactSerialFrameConstants.StartMarkerFirstByte
            || encoded[1]
                != CompactSerialFrameConstants.StartMarkerSecondByte)
        {
            throw new InvalidDataException(
                "The compact serial frame start marker is invalid.");
        }

        if (encoded[2]
            != CompactSerialFrameConstants.ProtocolVersion)
        {
            throw new InvalidDataException(
                $"Compact serial protocol version {encoded[2]} is not "
                + "supported.");
        }

        int payloadLength =
            encoded[5];

        int expectedFrameLength =
            CompactSerialFrameConstants.FrameOverheadLength
            + payloadLength;

        if (encoded.Length
            != expectedFrameLength)
        {
            throw new InvalidDataException(
                $"The compact serial frame length is {encoded.Length} "
                + $"bytes, but its payload length requires "
                + $"{expectedFrameLength} bytes.");
        }

        ushort expectedCrc =
            BinaryPrimitives.ReadUInt16BigEndian(
                encoded.Slice(
                    6 + payloadLength,
                    sizeof(ushort)));

        ushort actualCrc =
            Crc16CcittFalse.Calculate(
                encoded.Slice(
                    2,
                    4 + payloadLength));

        if (actualCrc
            != expectedCrc)
        {
            throw new InvalidDataException(
                $"The compact serial frame CRC is invalid. Expected "
                + $"0x{expectedCrc:X4}, calculated 0x{actualCrc:X4}.");
        }

        return new CompactSerialFrame(
            encoded[3],
            encoded[4],
            encoded.Slice(
                6,
                payloadLength));
    }
}