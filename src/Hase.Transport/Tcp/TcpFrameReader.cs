using System.Buffers.Binary;

namespace Hase.Transport.Tcp;

/// <summary>
/// Reads length-prefixed frames from a TCP byte stream.
///
/// Each frame consists of a four-byte unsigned big-endian payload length
/// followed by the payload bytes.
/// </summary>
public static class TcpFrameReader
{
    /// <summary>
    /// Reads one complete frame payload from the supplied stream.
    /// </summary>
    /// <param name="stream">
    /// Stream containing TCP transport frames.
    /// </param>
    /// <param name="maximumPayloadLength">
    /// Maximum accepted payload length in bytes.
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the operation.
    /// </param>
    public static async Task<byte[]> ReadAsync(
        Stream stream,
        int maximumPayloadLength,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            stream);

        if (maximumPayloadLength < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumPayloadLength),
                maximumPayloadLength,
                "The maximum payload length must not be negative.");
        }

        byte[] header =
            new byte[TcpFrameCodec.HeaderLength];

        await ReadExactlyAsync(
            stream,
            header,
            cancellationToken);

        uint encodedPayloadLength =
            BinaryPrimitives.ReadUInt32BigEndian(
                header);

        if (encodedPayloadLength > int.MaxValue)
        {
            throw new InvalidDataException(
                "The TCP frame payload length exceeds the supported size.");
        }

        int payloadLength =
            (int)encodedPayloadLength;

        if (payloadLength > maximumPayloadLength)
        {
            throw new InvalidDataException(
                $"The TCP frame payload length {payloadLength} exceeds "
                + $"the configured maximum of {maximumPayloadLength} bytes.");
        }

        if (payloadLength == 0)
        {
            return Array.Empty<byte>();
        }

        byte[] payload =
            new byte[payloadLength];

        await ReadExactlyAsync(
            stream,
            payload,
            cancellationToken);

        return payload;
    }

    private static async Task ReadExactlyAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        int offset =
            0;

        while (offset < buffer.Length)
        {
            int bytesRead =
                await stream.ReadAsync(
                    buffer.AsMemory(
                        offset,
                        buffer.Length - offset),
                    cancellationToken);

            if (bytesRead == 0)
            {
                throw new EndOfStreamException(
                    "The stream ended before the complete TCP frame was received.");
            }

            offset +=
                bytesRead;
        }
    }
}