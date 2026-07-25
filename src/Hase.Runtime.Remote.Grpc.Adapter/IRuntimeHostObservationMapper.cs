using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps normalized northbound runtime-host observations to version 1 stream
/// messages.
/// </summary>
public interface IRuntimeHostObservationMapper
{
    /// <summary>
    /// Maps one generation-scoped observation.
    /// </summary>
    GrpcV1.ObserveResponse Map(
        Northbound.RuntimeHostObservation observation);
}
