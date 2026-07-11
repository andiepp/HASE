using Hase.Runtime.Runtime;

namespace Hase.Runtime.Connections;

/// <summary>
/// Describes a connection-status change for a runtime endpoint.
/// </summary>
public sealed record EndpointConnectionStatusChanged(
    RuntimeEndpoint Endpoint,
    EndpointConnectionStatus PreviousStatus,
    EndpointConnectionStatus CurrentStatus);