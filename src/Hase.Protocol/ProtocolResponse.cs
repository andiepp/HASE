namespace Hase.Protocol;

/// <summary>
/// Base type for HASE protocol response messages.
/// </summary>
public abstract record ProtocolResponse(
    ProtocolVersion Version,
    ProtocolMessageType MessageType,
    CorrelationId CorrelationId)
    : ProtocolMessage(
        Version,
        ProtocolMessageRole.Response,
        MessageType,
        CorrelationId);