namespace Hase.Protocol;

/// <summary>
/// Requests discovery of all instruments available on an endpoint.
/// </summary>
public sealed record DiscoverRequest(
    CorrelationId CorrelationId)
    : ProtocolMessage(
        ProtocolVersion.Current,
        ProtocolMessageRole.Request,
        ProtocolMessageType.DiscoverRequest,
        CorrelationId);