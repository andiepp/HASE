using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps normalized northbound observation kinds to the version 1 remote
/// contract.
/// </summary>
public interface IRuntimeHostObservationKindMapper
{
    /// <summary>
    /// Maps one normalized observation kind.
    /// </summary>
    GrpcV1.RuntimeHostObservationKind Map(
        Northbound.RuntimeHostObservationKind kind);
}
