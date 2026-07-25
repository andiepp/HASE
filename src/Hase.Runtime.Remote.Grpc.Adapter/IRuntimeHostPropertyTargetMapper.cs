using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps a version 1 remote Property target to the generation-scoped
/// northbound runtime-host target.
/// </summary>
public interface IRuntimeHostPropertyTargetMapper
{
    /// <summary>
    /// Maps one remote Property target.
    /// </summary>
    Northbound.RuntimeHostPropertyTarget Map(
        GrpcV1.PropertyTarget source);
}
