using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps cached northbound Property results to the version 1 remote contract.
/// </summary>
public interface IRuntimeHostCachedPropertyResultMapper
{
    /// <summary>
    /// Maps one normalized cached Property result.
    /// </summary>
    GrpcV1.CachedPropertyResult Map(
        Northbound.RuntimeHostCachedPropertyResult result);
}
