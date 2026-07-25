using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps normalized Event-occurred observation payloads to the version 1 remote
/// contract.
/// </summary>
public interface IRuntimeHostEventOccurredObservationPayloadMapper
{
    /// <summary>
    /// Maps one Event-occurred payload.
    /// </summary>
    GrpcV1.EventOccurredObservation Map(
        Northbound.RuntimeHostEventOccurredObservationPayload payload);
}
