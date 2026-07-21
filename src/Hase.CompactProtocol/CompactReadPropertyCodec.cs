namespace Hase.CompactProtocol;

/// <summary>
/// Encodes and decodes Compact Serial Protocol Version 1 property-read
/// messages.
/// </summary>
internal static class CompactReadPropertyCodec
{
    public static CompactSerialFrame EncodeRequest(
        CompactReadPropertyRequest request)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        return new CompactSerialFrame(
            (byte)CompactSerialMessageType.ReadPropertyRequest,
            request.CorrelationId,
            payload:
            [
                request.PropertyId
            ]);
    }

    public static CompactReadPropertyRequest DecodeRequest(
        CompactSerialFrame frame)
    {
        ArgumentNullException.ThrowIfNull(
            frame);

        ValidateMessageType(
            frame,
            CompactSerialMessageType.ReadPropertyRequest);

        ReadOnlySpan<byte> payload =
            frame.Payload.Span;

        if (payload.Length != 1)
        {
            throw new InvalidDataException(
                "A compact read-property request must contain exactly "
                + "one property identifier byte.");
        }

        try
        {
            return new CompactReadPropertyRequest(
                frame.CorrelationId,
                payload[0]);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidDataException(
                "The compact read-property request contains invalid "
                + "identifier data.",
                exception);
        }
    }

    public static CompactSerialFrame EncodeResponse(
        CompactReadPropertyResponse response)
    {
        ArgumentNullException.ThrowIfNull(
            response);

        byte[] payload =
            new byte[
                2
                + response.Value.Length];

        payload[0] =
            response.PropertyId;

        payload[1] =
            (byte)response.Status;

        response.Value.Span.CopyTo(
            payload.AsSpan(
                2));

        return new CompactSerialFrame(
            (byte)CompactSerialMessageType.ReadPropertyResponse,
            response.CorrelationId,
            payload);
    }

    public static CompactReadPropertyResponse DecodeResponse(
        CompactSerialFrame frame)
    {
        ArgumentNullException.ThrowIfNull(
            frame);

        ValidateMessageType(
            frame,
            CompactSerialMessageType.ReadPropertyResponse);

        ReadOnlySpan<byte> payload =
            frame.Payload.Span;

        if (payload.Length < 2)
        {
            throw new InvalidDataException(
                "A compact read-property response must contain a property "
                + "identifier and a status byte.");
        }

        CompactPropertyReadStatus status =
            (CompactPropertyReadStatus)payload[1];

        if (!Enum.IsDefined(
                status))
        {
            throw new InvalidDataException(
                $"Compact property-read status 0x{payload[1]:X2} "
                + "is not supported.");
        }

        try
        {
            return new CompactReadPropertyResponse(
                frame.CorrelationId,
                payload[0],
                status,
                payload[2..].ToArray());
        }
        catch (
            Exception exception)
            when (exception is ArgumentException)
        {
            throw new InvalidDataException(
                "The compact read-property response contains invalid "
                + "identifier, status, or value data.",
                exception);
        }
    }

    private static void ValidateMessageType(
        CompactSerialFrame frame,
        CompactSerialMessageType expected)
    {
        if (frame.MessageType
            != (byte)expected)
        {
            throw new InvalidDataException(
                $"Compact serial message type 0x{frame.MessageType:X2} "
                + $"is not {expected}.");
        }
    }
}