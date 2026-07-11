using Hase.Core.Domain.Identity;

namespace Hase.Protocol;

/// <summary>
/// Requests the current value of a property.
/// </summary>
public sealed record ReadPropertyRequest(
    CorrelationId CorrelationId,
    InstrumentId InstrumentId,
    PropertyId PropertyId)
    : ProtocolMessage(
        ProtocolVersion.Current,
        ProtocolMessageRole.Request,
        ProtocolMessageType.ReadPropertyRequest,
        CorrelationId);