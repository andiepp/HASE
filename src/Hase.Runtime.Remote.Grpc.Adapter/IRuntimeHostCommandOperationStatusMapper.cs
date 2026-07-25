using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps normalized northbound Command operation statuses to the version 1
/// remote contract.
/// </summary>
public interface IRuntimeHostCommandOperationStatusMapper
{
    /// <summary>
    /// Maps one normalized Command operation status.
    /// </summary>
    GrpcV1.CommandOperationStatus Map(
        Northbound.RuntimeHostCommandOperationStatus status);
}
