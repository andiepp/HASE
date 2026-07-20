namespace Hase.CompactProtocol;

/// <summary>
/// Encodes and decodes Compact Serial Protocol Version 1 command execution
/// messages.
/// </summary>
internal static class CompactExecuteCommandCodec
{
    public static CompactSerialFrame EncodeRequest(
        CompactExecuteCommandRequest request)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        return new CompactSerialFrame(
            (byte)CompactSerialMessageType.ExecuteCommandRequest,
            request.CorrelationId,
            payload:
            [
                request.CommandId
            ]);
    }

    public static CompactExecuteCommandRequest DecodeRequest(
        CompactSerialFrame frame)
    {
        ArgumentNullException.ThrowIfNull(
            frame);

        ValidateMessageType(
            frame,
            CompactSerialMessageType.ExecuteCommandRequest);

        ReadOnlySpan<byte> payload =
            frame.Payload.Span;

        if (payload.Length != 1)
        {
            throw new InvalidDataException(
                "A compact execute-command request must contain exactly "
                + "one command identifier byte.");
        }

        try
        {
            return new CompactExecuteCommandRequest(
                frame.CorrelationId,
                payload[0]);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidDataException(
                "The compact execute-command request contains invalid "
                + "identifier data.",
                exception);
        }
    }

    public static CompactSerialFrame EncodeResponse(
        CompactExecuteCommandResponse response)
    {
        ArgumentNullException.ThrowIfNull(
            response);

        return new CompactSerialFrame(
            (byte)CompactSerialMessageType.ExecuteCommandResponse,
            response.CorrelationId,
            payload:
            [
                response.CommandId,
                (byte)response.Status
            ]);
    }

    public static CompactExecuteCommandResponse DecodeResponse(
        CompactSerialFrame frame)
    {
        ArgumentNullException.ThrowIfNull(
            frame);

        ValidateMessageType(
            frame,
            CompactSerialMessageType.ExecuteCommandResponse);

        ReadOnlySpan<byte> payload =
            frame.Payload.Span;

        if (payload.Length != 2)
        {
            throw new InvalidDataException(
                "A compact execute-command response must contain exactly "
                + "one command identifier byte and one status byte.");
        }

        CompactCommandExecutionStatus status =
            (CompactCommandExecutionStatus)payload[1];

        if (!Enum.IsDefined(
                status))
        {
            throw new InvalidDataException(
                $"Compact command execution status 0x{payload[1]:X2} "
                + "is not supported.");
        }

        try
        {
            return new CompactExecuteCommandResponse(
                frame.CorrelationId,
                payload[0],
                status);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidDataException(
                "The compact execute-command response contains invalid "
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