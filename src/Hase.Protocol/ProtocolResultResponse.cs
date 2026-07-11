namespace Hase.Protocol;

/// <summary>
/// Base type for HASE protocol responses that report an operation result.
/// </summary>
public abstract record ProtocolResultResponse(
    ProtocolVersion Version,
    ProtocolMessageType MessageType,
    CorrelationId CorrelationId,
    ProtocolResult Result)
    : ProtocolResponse(
        Version,
        MessageType,
        CorrelationId);