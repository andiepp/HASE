namespace Hase.CompactProtocol;

/// <summary>
/// Encodes and decodes unsolicited Compact Serial Protocol Version 1 event
/// notifications.
/// </summary>
internal static class CompactEventNotificationCodec
{
    public static CompactSerialFrame Encode(
        CompactEventNotification notification)
    {
        ArgumentNullException.ThrowIfNull(
            notification);

        byte[] payload =
            new byte[
                1
                + notification.Value.Length];

        payload[0] =
            notification.EventId;

        notification.Value.Span.CopyTo(
            payload.AsSpan(
                1));

        return new CompactSerialFrame(
            (byte)CompactSerialMessageType.EventNotification,
            correlationId: 0x00,
            payload);
    }

    public static CompactEventNotification Decode(
        CompactSerialFrame frame)
    {
        ArgumentNullException.ThrowIfNull(
            frame);

        if (frame.MessageType
            != (byte)CompactSerialMessageType.EventNotification)
        {
            throw new InvalidDataException(
                $"Compact serial message type 0x{frame.MessageType:X2} "
                + $"is not {CompactSerialMessageType.EventNotification}.");
        }

        if (frame.CorrelationId != 0)
        {
            throw new InvalidDataException(
                "A compact event notification must use correlation "
                + "identifier zero.");
        }

        ReadOnlySpan<byte> payload =
            frame.Payload.Span;

        if (payload.Length < 1)
        {
            throw new InvalidDataException(
                "A compact event notification must contain an event "
                + "identifier.");
        }

        try
        {
            return new CompactEventNotification(
                payload[0],
                payload[1..].ToArray());
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidDataException(
                "The compact event notification contains an invalid event "
                + "identifier.",
                exception);
        }
    }
}