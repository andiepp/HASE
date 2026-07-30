namespace Hase.CompactProtocol;

/// <summary>
/// Contains one decoded Compact frame and its exact owned wire bytes.
/// </summary>
internal sealed record CompactSerialFrameReadResult(
    CompactSerialFrame Frame,
    ReadOnlyMemory<byte> EncodedBytes);
