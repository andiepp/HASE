using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps a version 1 remote Command target to the generation-scoped
/// northbound runtime-host target.
/// </summary>
public interface IRuntimeHostCommandTargetMapper
{
    /// <summary>
    /// Maps one remote Command target.
    /// </summary>
    Northbound.RuntimeHostCommandTarget Map(
        GrpcV1.CommandTarget source);
}
