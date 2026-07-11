using Hase.Core.Domain.Identity;

namespace Hase.Protocol;

/// <summary>
/// Requests the descriptor for a specific endpoint.
/// </summary>
public sealed record ReadEndpointDescriptorRequest(
    CorrelationId CorrelationId,
    EndpointId EndpointId)
    : ProtocolMessage(
        ProtocolVersion.Current,
        ProtocolMessageRole.Request,
        ProtocolMessageType.ReadEndpointDescriptorRequest,
        CorrelationId);