using Hase.Core.Domain.Endpoints;

namespace Hase.Protocol;

/// <summary>
/// Returns the descriptor of a specific endpoint.
/// </summary>
public sealed record ReadEndpointDescriptorResponse(
    CorrelationId CorrelationId,
    ProtocolResult Result,
    EndpointDescriptor? Descriptor)
    : ProtocolResultResponse(
        ProtocolVersion.Current,
        ProtocolMessageType.ReadEndpointDescriptorResponse,
        CorrelationId,
        Result);