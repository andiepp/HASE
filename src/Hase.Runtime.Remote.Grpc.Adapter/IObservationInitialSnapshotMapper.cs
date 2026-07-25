using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps one authoritative observation-subscription boundary to the mandatory
/// first version 1 stream message.
/// </summary>
public interface IObservationInitialSnapshotMapper
{
    /// <summary>
    /// Maps the snapshot and exact subscription-local sequence returned by one
    /// observation subscription.
    /// </summary>
    GrpcV1.ObserveResponse Map(
        Northbound.PublishedRuntimeHostSnapshot snapshot,
        Northbound.RuntimeHostObservationSequence snapshotSequence);
}
