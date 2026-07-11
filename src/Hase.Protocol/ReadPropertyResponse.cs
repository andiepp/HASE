using Hase.Core.Domain.Properties;

namespace Hase.Protocol;

/// <summary>
/// Returns the current value of a property.
/// </summary>
public sealed record ReadPropertyResponse(
    CorrelationId CorrelationId,
    ProtocolResult Result,
    PropertyValue? PropertyValue)
    : ProtocolResultResponse(
        ProtocolVersion.Current,
        ProtocolMessageType.ReadPropertyResponse,
        CorrelationId,
        Result);