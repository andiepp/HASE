using DomainData = global::Hase.Core.Domain.Data;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps the closed set of data descriptor variants to the version 1 remote
/// contract union.
/// </summary>
public sealed class DataDescriptorMapper
    : IDataDescriptorMapper
{
    private readonly INumericDataDescriptorMapper numericDataDescriptorMapper;

    /// <summary>
    /// Initializes the mapper.
    /// </summary>
    public DataDescriptorMapper(
        INumericDataDescriptorMapper numericDataDescriptorMapper)
    {
        this.numericDataDescriptorMapper =
            numericDataDescriptorMapper
            ?? throw new ArgumentNullException(
                nameof(numericDataDescriptorMapper));
    }

    /// <inheritdoc />
    public GrpcV1.DataDescriptor Map(
        DomainData.DataDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(
            descriptor);

        return descriptor switch
        {
            DomainData.BooleanDataDescriptor =>
                new GrpcV1.DataDescriptor
                {
                    BooleanDescriptor =
                        new GrpcV1.BooleanDataDescriptor()
                },
            DomainData.StringDataDescriptor =>
                new GrpcV1.DataDescriptor
                {
                    StringDescriptor =
                        new GrpcV1.StringDataDescriptor()
                },
            DomainData.NumericDataDescriptor numericDescriptor =>
                new GrpcV1.DataDescriptor
                {
                    Numeric =
                        numericDataDescriptorMapper.Map(
                            numericDescriptor)
                        ?? throw new InvalidOperationException(
                            "The numeric data descriptor mapper returned null.")
                },
            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(descriptor),
                    descriptor,
                    "The data descriptor type is not supported.")
        };
    }
}
