using DomainData = global::Hase.Core.Domain.Data;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps one engineering quantity to its version 1 remote contract
/// representation.
/// </summary>
public interface IQuantityMapper
{
    /// <summary>
    /// Maps one immutable engineering quantity.
    /// </summary>
    GrpcV1.Quantity Map(
        DomainData.Quantity quantity);
}
