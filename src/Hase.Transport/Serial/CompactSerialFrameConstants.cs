namespace Hase.Transport.Serial;

/// <summary>
/// Defines the frozen Compact Serial Protocol Version 1 frame limits.
/// </summary>
internal static class CompactSerialFrameConstants
{
    public const byte StartMarkerFirstByte =
        0x48;

    public const byte StartMarkerSecondByte =
        0x53;

    public const byte ProtocolVersion =
        0x01;

    public const int MaximumPayloadLength =
        byte.MaxValue;

    public const int FrameOverheadLength =
        8;

    public const int MaximumFrameLength =
        FrameOverheadLength
        + MaximumPayloadLength;
}