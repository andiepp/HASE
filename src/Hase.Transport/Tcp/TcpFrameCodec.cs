using System.Buffers.Binary;

namespace Hase.Transport.Tcp;

/// <summary>
/// Encodes byte payloads using the HASE TCP transport framing profile.
///
/// Each frame consists of a four-byte unsigned big-endian payload length
/// followed by the payload bytes.
/// </summary>
public static class TcpFrameCodec
{
    /// <summary>
    /// Number of bytes used by the TCP frame header.
    /// </summary>
    public const int HeaderLength =
        sizeof(uint);

    /// <summary>
    /// Encodes a payload as a length-prefixed TCP frame.
    /// </summary>
    public static byte[] Encode(
        byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(
            payload);

        byte[] frame =
            new byte[
                HeaderLength
                + payload.Length];

        BinaryPrimitives.WriteUInt32BigEndian(
            frame.AsSpan(
                0,
                HeaderLength),
            checked((uint)payload.Length));

        payload.CopyTo(
            frame,
            HeaderLength);

        return frame;
    }
}
