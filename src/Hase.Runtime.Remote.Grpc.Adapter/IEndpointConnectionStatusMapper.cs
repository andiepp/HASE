using RuntimeConnections = global::Hase.Runtime.Connections;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps one endpoint connection status to its version 1 remote contract
/// representation.
/// </summary>
public interface IEndpointConnectionStatusMapper
{
    /// <summary>
    /// Maps one captured endpoint connection status.
    /// </summary>
    GrpcV1.EndpointConnectionStatus Map(
        RuntimeConnections.EndpointConnectionStatus status);
}
