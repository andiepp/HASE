using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps one transport-independent endpoint snapshot to its version 1 remote
/// contract representation.
/// </summary>
public interface IRuntimeEndpointSnapshotMapper
{
    /// <summary>
    /// Maps one published endpoint snapshot.
    /// </summary>
    GrpcV1.PublishedRuntimeEndpointSnapshot Map(
        Northbound.PublishedRuntimeEndpointSnapshot snapshot);
}
