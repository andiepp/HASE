using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps normalized connection-status-changed observation payloads to the
/// version 1 remote contract.
/// </summary>
public interface IRuntimeHostConnectionStatusChangedObservationPayloadMapper
{
    /// <summary>
    /// Maps one connection-status-changed payload.
    /// </summary>
    GrpcV1.ConnectionStatusChangedObservation Map(
        Northbound.RuntimeHostConnectionStatusChangedObservationPayload
            payload);
}
