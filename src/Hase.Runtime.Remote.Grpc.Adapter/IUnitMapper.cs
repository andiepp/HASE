using DomainData = global::Hase.Core.Domain.Data;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps one engineering unit to its version 1 remote contract representation.
/// </summary>
public interface IUnitMapper
{
    /// <summary>
    /// Maps one immutable engineering unit.
    /// </summary>
    GrpcV1.Unit Map(
        DomainData.Unit unit);
}
