using Hase.Core.Domain.Identity;

namespace Hase.Protocol;

/// <summary>
/// Returns the identity of an endpoint and the identities of the
/// instruments available on that endpoint.
/// </summary>
public sealed record DiscoverResponse(
    CorrelationId CorrelationId,
    EndpointId EndpointId,
    IReadOnlyList<InstrumentId> InstrumentIds)
    : ProtocolResponse(
        ProtocolVersion.Current,
        ProtocolMessageType.DiscoverResponse,
        CorrelationId);