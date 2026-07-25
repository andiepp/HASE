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

    /// <summary>
    /// Maps one version 1 remote value to its normalized CLR representation.
    /// An unselected union represents null.
    /// </summary>
    object? MapToClr(
        GrpcV1.RemoteValue value);
}
