using DomainData = global::Hase.Core.Domain.Data;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps engineering units to the version 1 remote contract.
/// </summary>
public sealed class UnitMapper
    : IUnitMapper
{
    private readonly IQuantityMapper quantityMapper;

    /// <summary>
    /// Initializes the mapper.
    /// </summary>
    public UnitMapper(
        IQuantityMapper quantityMapper)
    {
        this.quantityMapper =
            quantityMapper
            ?? throw new ArgumentNullException(
                nameof(quantityMapper));
    }

    /// <inheritdoc />
    public GrpcV1.Unit Map(
        DomainData.Unit unit)
    {
        ArgumentNullException.ThrowIfNull(
            unit);

        GrpcV1.Quantity quantity =
            quantityMapper.Map(
                unit.Quantity)
            ?? throw new InvalidOperationException(
                "The quantity mapper returned null.");

        return new GrpcV1.Unit
        {
            Id =
                unit.Id,
            DisplayName =
                unit.DisplayName,
            Symbol =
                unit.Symbol,
            Quantity =
                quantity
        };
    }
}
