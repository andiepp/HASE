using Hase.Core.Domain.Identity;

namespace Hase.Protocol;

/// <summary>
/// Requests that a property be assigned a new value.
/// </summary>
public sealed record WritePropertyRequest(
    CorrelationId CorrelationId,
    InstrumentId InstrumentId,
    PropertyId PropertyId,
    object? Value)
    : ProtocolMessage(
        ProtocolVersion.Current,
        ProtocolMessageRole.Request,
        ProtocolMessageType.WritePropertyRequest,
        CorrelationId);