using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps published runtime Property snapshots to the version 1 remote
/// contract.
/// </summary>
public interface IPublishedRuntimePropertySnapshotMapper
{
    /// <summary>
    /// Maps one immutable published Property snapshot.
    /// </summary>
    GrpcV1.PublishedRuntimePropertySnapshot Map(
        Northbound.PublishedRuntimePropertySnapshot snapshot);
}
