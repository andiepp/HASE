using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps normalized Property-value-changed observation payloads to the version
/// 1 remote contract.
/// </summary>
public interface IRuntimeHostPropertyValueChangedObservationPayloadMapper
{
    /// <summary>
    /// Maps one Property-value-changed payload.
    /// </summary>
    GrpcV1.PropertyValueChangedObservation Map(
        Northbound.RuntimeHostPropertyValueChangedObservationPayload payload);
}
