using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps normalized northbound Command operation results to the version 1
/// remote contract.
/// </summary>
public interface IRuntimeHostCommandOperationResultMapper
{
    /// <summary>
    /// Maps one normalized Command operation result.
    /// </summary>
    GrpcV1.CommandOperationResult Map(
        Northbound.RuntimeHostCommandOperationResult result);
}
