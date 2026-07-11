namespace Hase.Protocol;

/// <summary>
/// Contains the wire-level metadata and serialized payload of a HASE
/// protocol message.
/// </summary>
public sealed record ProtocolEnvelope(
    ProtocolVersion Version,
    ProtocolMessageRole Role,
    ProtocolMessageType MessageType,
    CorrelationId CorrelationId,
    ReadOnlyMemory<byte> Payload)
{
    /// <summary>
    /// Gets the number of bytes contained in the serialized payload.
    /// </summary>
    public int PayloadLength => Payload.Length;
}