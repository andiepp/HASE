using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps normalized northbound Property operation statuses to the version 1
/// remote contract.
/// </summary>
public interface IRuntimeHostPropertyOperationStatusMapper
{
    /// <summary>
    /// Maps one normalized Property operation status.
    /// </summary>
    GrpcV1.PropertyOperationStatus Map(
        Northbound.RuntimeHostPropertyOperationStatus status);
}
