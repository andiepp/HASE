using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps supported normalized CLR values to the version 1 remote value union.
/// </summary>
public interface IRemoteValueMapper
{
    /// <summary>
    /// Maps one explicit supported value.
    /// </summary>
    GrpcV1.RemoteValue Map(
        object value);
}
