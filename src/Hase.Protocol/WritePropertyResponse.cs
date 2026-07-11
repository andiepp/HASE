using Hase.Core.Domain.Properties;

namespace Hase.Protocol;

/// <summary>
/// Reports the result of assigning a new value to a property.
/// </summary>
public sealed record WritePropertyResponse(
    CorrelationId CorrelationId,
    ProtocolResult Result,
    PropertyValue? PropertyValue)
    : ProtocolResultResponse(
        ProtocolVersion.Current,
        ProtocolMessageType.WritePropertyResponse,
        CorrelationId,
        Result);
