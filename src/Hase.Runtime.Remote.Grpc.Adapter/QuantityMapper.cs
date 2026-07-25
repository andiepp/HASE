using DomainData = global::Hase.Core.Domain.Data;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps engineering quantities to the version 1 remote contract.
/// </summary>
public sealed class QuantityMapper
    : IQuantityMapper
{
    /// <inheritdoc />
    public GrpcV1.Quantity Map(
        DomainData.Quantity quantity)
    {
        ArgumentNullException.ThrowIfNull(
            quantity);

        return new GrpcV1.Quantity
        {
            Id =
                quantity.Id,
            DisplayName =
                quantity.DisplayName
        };
    }
}
