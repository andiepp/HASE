using Hase.Transport.Serial;

namespace Hase.CompactProtocol;

/// <summary>
/// Reads complete Compact Serial Protocol Version 1 frames from a fragmented
/// serial byte stream.
/// </summary>
internal sealed class CompactSerialFrameReader
{
    private readonly ISerialByteStream _stream;
    private readonly bool _rejectUnsupportedProtocolVersion;

    public CompactSerialFrameReader(
        ISerialByteStream stream)
        : this(
            stream,
            rejectUnsupportedProtocolVersion: false)
    {
    }

    internal CompactSerialFrameReader(
        ISerialByteStream stream,
        bool rejectUnsupportedProtocolVersion)
    {
        _stream =
            stream
            ?? throw new ArgumentNullException(
                nameof(stream));

        _rejectUnsupportedProtocolVersion =
            rejectUnsupportedProtocolVersion;
    }

    /// <summary>
    /// Scans through non-frame bytes and returns the next valid complete frame.
    /// Corrupted complete frames are discarded. Unsupported protocol versions
    /// are either discarded or rejected according to this reader's policy.
    /// </summary>
    public async Task<CompactSerialFrame> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        return await ReadCoreAsync(
            captureEncodedBytes: false,
            cancellationToken);
    }

    /// <summary>
    /// Returns the next valid decoded frame together with an owned copy of its
    /// exact complete wire representation.
    /// </summary>
    internal async Task<CompactSerialFrameReadResult> ReadWithBytesAsync(
        CancellationToken cancellationToken = default)
    {
        CompactSerialFrame frame;
        byte[] encodedBytes;

        (frame, encodedBytes) =
            await ReadCoreWithBytesAsync(
                cancellationToken);

        return new CompactSerialFrameReadResult(
            frame,
            encodedBytes);
    }

    private async Task<CompactSerialFrame> ReadCoreAsync(
        bool captureEncodedBytes,
        CancellationToken cancellationToken)
    {
        (CompactSerialFrame frame, _) =
            await ReadCoreWithBytesAsync(
                cancellationToken,
                captureEncodedBytes);

        return frame;
    }

    private Task<(CompactSerialFrame Frame, byte[] EncodedBytes)>
        ReadCoreWithBytesAsync(
            CancellationToken cancellationToken)
    {
        return ReadCoreWithBytesAsync(
            cancellationToken,
            captureEncodedBytes: true);
    }

    private async Task<(CompactSerialFrame Frame, byte[] EncodedBytes)>
        ReadCoreWithBytesAsync(
            CancellationToken cancellationToken,
            bool captureEncodedBytes)
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

            if (_rejectUnsupportedProtocolVersion
                && encoded[2]
                    != CompactSerialFrameConstants.ProtocolVersion)
            {
                throw new CompactProtocolVersionNotSupportedException(
                    encoded[2],
                    CompactSerialFrameConstants.ProtocolVersion);
            }

            try
            {
                CompactSerialFrame frame =
                    CompactSerialFrameCodec.Decode(
                        encoded.AsSpan(
                            0,
                            frameLength));

                byte[] encodedBytes =
                    captureEncodedBytes
                        ? encoded
                            .AsSpan(
                                0,
                                frameLength)
                            .ToArray()
                        : [];

                return (
                    frame,
                    encodedBytes);
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
