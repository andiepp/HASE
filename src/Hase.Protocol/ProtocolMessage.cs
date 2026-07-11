namespace Hase.Protocol;

/// <summary>
/// Base type for all HASE protocol messages.
/// </summary>
public abstract record ProtocolMessage(
    ProtocolVersion Version,
    ProtocolMessageRole Role,
    ProtocolMessageType MessageType,
    CorrelationId CorrelationId);
