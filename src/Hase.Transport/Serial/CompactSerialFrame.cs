namespace Hase.Transport.Serial;

/// <summary>
/// Represents one complete Compact Serial Protocol Version 1 frame.
/// </summary>
internal sealed class CompactSerialFrame
{
    private readonly byte[] _payload;

    public CompactSerialFrame(
        byte messageType,
        byte correlationId,
        ReadOnlySpan<byte> payload)
    {
        if (payload.Length
            > CompactSerialFrameConstants.MaximumPayloadLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(payload),
                payload.Length,
                $"The compact serial payload must not exceed "
                + $"{CompactSerialFrameConstants.MaximumPayloadLength} "
                + "bytes.");
        }

        MessageType =
            messageType;

        CorrelationId =
            correlationId;

        _payload =
            payload.ToArray();
    }

    /// <summary>
    /// Gets the compact protocol message type.
    /// </summary>
    public byte MessageType
    {
        get;
    }

    /// <summary>
    /// Gets the request/response correlation identifier. Zero is reserved for
    /// future unsolicited notifications.
    /// </summary>
    public byte CorrelationId
    {
        get;
    }

    /// <summary>
    /// Gets the immutable message-specific payload.
    /// </summary>
    public ReadOnlyMemory<byte> Payload =>
        _payload;
}