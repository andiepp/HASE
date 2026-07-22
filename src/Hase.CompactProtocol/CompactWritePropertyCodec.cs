namespace Hase.CompactProtocol;

/// <summary>
/// Encodes and decodes Compact Serial Protocol Version 1 property-write
/// messages.
/// </summary>
internal static class CompactWritePropertyCodec
{
    public static CompactSerialFrame EncodeRequest(
        CompactWritePropertyRequest request)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        byte[] payload =
            new byte[
                1
                + request.Value.Length];

        payload[0] =
            request.PropertyId;

        request.Value.Span.CopyTo(
            payload.AsSpan(
                1));

        return new CompactSerialFrame(
            (byte)CompactSerialMessageType.WritePropertyRequest,
            request.CorrelationId,
            payload);
    }

    public static CompactWritePropertyRequest DecodeRequest(
        CompactSerialFrame frame)
    {
        ArgumentNullException.ThrowIfNull(
            frame);

        ValidateMessageType(
            frame,
            CompactSerialMessageType.WritePropertyRequest);

        ReadOnlySpan<byte> payload =
            frame.Payload.Span;

        if (payload.Length < 2)
        {
            throw new InvalidDataException(
                "A compact write-property request must contain a property "
                + "identifier and value bytes.");
        }

        try
        {
            return new CompactWritePropertyRequest(
                frame.CorrelationId,
                payload[0],
                payload[1..].ToArray());
        }
        catch (
            Exception exception)
            when (exception is ArgumentException)
        {
            throw new InvalidDataException(
                "The compact write-property request contains invalid "
                + "identifier or value data.",
                exception);
        }
    }

    public static CompactSerialFrame EncodeResponse(
        CompactWritePropertyResponse response)
    {
        ArgumentNullException.ThrowIfNull(
            response);

        return new CompactSerialFrame(
            (byte)CompactSerialMessageType.WritePropertyResponse,
            response.CorrelationId,
            payload:
            [
                response.PropertyId,
                (byte)response.Status
            ]);
    }

    public static CompactWritePropertyResponse DecodeResponse(
        CompactSerialFrame frame)
    {
        ArgumentNullException.ThrowIfNull(
            frame);

        ValidateMessageType(
            frame,
            CompactSerialMessageType.WritePropertyResponse);

        ReadOnlySpan<byte> payload =
            frame.Payload.Span;

        if (payload.Length != 2)
        {
            throw new InvalidDataException(
                "A compact write-property response must contain exactly "
                + "one property identifier byte and one status byte.");
        }

        CompactPropertyWriteStatus status =
            (CompactPropertyWriteStatus)payload[1];

        if (!Enum.IsDefined(
                status))
        {
            throw new InvalidDataException(
                $"Compact property-write status 0x{payload[1]:X2} "
                + "is not supported.");
        }

        try
        {
            return new CompactWritePropertyResponse(
                frame.CorrelationId,
                payload[0],
                status);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidDataException(
                "The compact write-property response contains invalid "
                + "identifier data.",
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