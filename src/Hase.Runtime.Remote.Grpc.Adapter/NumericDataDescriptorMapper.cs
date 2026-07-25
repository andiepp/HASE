using DomainData = global::Hase.Core.Domain.Data;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps numeric data descriptors to the version 1 remote contract.
/// </summary>
public sealed class NumericDataDescriptorMapper
    : INumericDataDescriptorMapper
{
    private readonly IQuantityMapper quantityMapper;
    private readonly IUnitMapper unitMapper;

    /// <summary>
    /// Initializes the mapper.
    /// </summary>
    public NumericDataDescriptorMapper(
        IQuantityMapper quantityMapper,
        IUnitMapper unitMapper)
    {
        this.quantityMapper =
            quantityMapper
            ?? throw new ArgumentNullException(
                nameof(quantityMapper));

        this.unitMapper =
            unitMapper
            ?? throw new ArgumentNullException(
                nameof(unitMapper));
    }

    /// <inheritdoc />
    public GrpcV1.NumericDataDescriptor Map(
        DomainData.NumericDataDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(
            descriptor);

        GrpcV1.Quantity quantity =
            quantityMapper.Map(
                descriptor.Quantity)
            ?? throw new InvalidOperationException(
                "The quantity mapper returned null.");

        GrpcV1.Unit nativeUnit =
            unitMapper.Map(
                descriptor.NativeUnit)
            ?? throw new InvalidOperationException(
                "The unit mapper returned null.");

        var result =
            new GrpcV1.NumericDataDescriptor
            {
                Quantity =
                    quantity,
                NativeUnit =
                    nativeUnit
            };

        if (descriptor.Range is not null)
        {
            result.Range =
                new GrpcV1.ValueRange
                {
                    Minimum =
                        descriptor.Range.Minimum,
                    Maximum =
                        descriptor.Range.Maximum
                };
        }

        if (descriptor.Resolution is not null)
        {
            result.Resolution =
                new GrpcV1.Resolution
                {
                    Value =
                        descriptor.Resolution.Value
                };
        }

        return result;
    }
}
