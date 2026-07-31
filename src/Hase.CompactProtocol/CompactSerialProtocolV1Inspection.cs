namespace Hase.CompactProtocol;

/// <summary>
/// Exposes frozen Compact Serial Protocol V1 wire facts for read-only
/// diagnostic inspection. It does not encode, decode, or own connections.
/// </summary>
public static class CompactSerialProtocolV1Inspection
{
    public const byte StartMarkerFirstByte =
        CompactSerialFrameConstants.StartMarkerFirstByte;

    public const byte StartMarkerSecondByte =
        CompactSerialFrameConstants.StartMarkerSecondByte;

    public const byte ProtocolVersion =
        CompactSerialFrameConstants.ProtocolVersion;

    public const int FrameOverheadLength =
        CompactSerialFrameConstants.FrameOverheadLength;

    public const int MaximumPayloadLength =
        CompactSerialFrameConstants.MaximumPayloadLength;

    public static bool TryGetMessageTypeName(
        byte encodedMessageType,
        out string name)
    {
        CompactSerialMessageType messageType =
            (CompactSerialMessageType)encodedMessageType;

        if (!Enum.IsDefined(
                messageType))
        {
            name =
                string.Empty;
            return false;
        }

        name =
            messageType.ToString();
        return true;
    }

    public static bool RequiresZeroCorrelationId(
        byte encodedMessageType)
    {
        return encodedMessageType
            == (byte)CompactSerialMessageType.EventNotification;
    }

    public static bool RequiresNonZeroCorrelationId(
        byte encodedMessageType)
    {
        return TryGetMessageTypeName(
                encodedMessageType,
                out _)
            && !RequiresZeroCorrelationId(
                encodedMessageType);
    }

    public static ushort CalculateCrc(
        ReadOnlySpan<byte> coveredBytes)
    {
        return Crc16CcittFalse.Calculate(
            coveredBytes);
    }
}
