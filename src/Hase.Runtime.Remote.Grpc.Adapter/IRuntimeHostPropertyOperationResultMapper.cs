using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps authoritative northbound Property operation results to the version 1
/// remote contract.
/// </summary>
public interface IRuntimeHostPropertyOperationResultMapper
{
    /// <summary>
    /// Maps one normalized authoritative Property operation result.
    /// </summary>
    GrpcV1.PropertyOperationResult Map(
        Northbound.RuntimeHostPropertyOperationResult result);
}
