namespace Hase.Transport.Serial;

/// <summary>
/// Reads complete Compact Serial Protocol Version 1 frames from a fragmented
/// serial byte stream.
/// </summary>
internal sealed class CompactSerialFrameReader
{
    private readonly ISerialByteStream _stream;

    public CompactSerialFrameReader(
        ISerialByteStream stream)
    {
        _stream =
            stream
            ?? throw new ArgumentNullException(
                nameof(stream));
    }

    /// <summary>
    /// Scans through non-frame bytes and returns the next valid complete frame.
    /// Corrupted or unsupported complete frames are discarded.
    /// </summary>
    public async Task<CompactSerialFrame> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var encoded =
            new byte[
                CompactSerialFrameConstants.MaximumFrameLength];

        while (true)
        {
            await ReadStartMarkerAsync(
                cancellationToken);

            encoded[0] =
                CompactSerialFrameConstants.StartMarkerFirstByte;

            encoded[1] =
                CompactSerialFrameConstants.StartMarkerSecondByte;

            await ReadExactlyAsync(
                encoded.AsMemory(
                    2,
                    4),
                cancellationToken);

            int payloadLength =
                encoded[5];

            int remainingLength =
                payloadLength
                + sizeof(ushort);

            await ReadExactlyAsync(
                encoded.AsMemory(
                    6,
                    remainingLength),
                cancellationToken);

            int frameLength =
                CompactSerialFrameConstants.FrameOverheadLength
                + payloadLength;

            try
            {
                return CompactSerialFrameCodec.Decode(
                    encoded.AsSpan(
                        0,
                        frameLength));
            }
            catch (InvalidDataException)
            {
                // The complete candidate was consumed. Resume scanning for
                // the next start marker without delivering the invalid frame.
            }
        }
    }

    private async Task ReadStartMarkerAsync(
        CancellationToken cancellationToken)
    {
        var singleByte =
            new byte[1];

        bool firstByteFound =
            false;

        while (true)
        {
            await ReadExactlyAsync(
                singleByte,
                cancellationToken);

            byte value =
                singleByte[0];

            if (!firstByteFound)
            {
                firstByteFound =
                    value
                    == CompactSerialFrameConstants
                        .StartMarkerFirstByte;

                continue;
            }

            if (value
                == CompactSerialFrameConstants.StartMarkerSecondByte)
            {
                return;
            }

            firstByteFound =
                value
                == CompactSerialFrameConstants.StartMarkerFirstByte;
        }
    }

    private async Task ReadExactlyAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        int totalBytesRead =
            0;

        while (totalBytesRead < buffer.Length)
        {
            int bytesRead =
                await _stream.ReadAsync(
                    buffer[totalBytesRead..],
                    cancellationToken);

            if (bytesRead == 0)
            {
                throw new EndOfStreamException(
                    "The serial byte stream ended before a complete "
                    + "compact serial frame was received.");
            }

            totalBytesRead +=
                bytesRead;
        }
    }
}