using DomainProperties = global::Hase.Core.Domain.Properties;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps Property descriptors to the version 1 remote contract.
/// </summary>
public sealed class PropertyDescriptorMapper
    : IPropertyDescriptorMapper
{
    private readonly IDataDescriptorMapper dataDescriptorMapper;

    /// <summary>
    /// Initializes the mapper.
    /// </summary>
    public PropertyDescriptorMapper(
        IDataDescriptorMapper dataDescriptorMapper)
    {
        this.dataDescriptorMapper =
            dataDescriptorMapper
            ?? throw new ArgumentNullException(
                nameof(dataDescriptorMapper));
    }

    /// <inheritdoc />
    public GrpcV1.PropertyDescriptor Map(
        DomainProperties.PropertyDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(
            descriptor);

        GrpcV1.DataDescriptor data =
            dataDescriptorMapper.Map(
                descriptor.Data)
            ?? throw new InvalidOperationException(
                "The data descriptor mapper returned null.");

        var result =
            new GrpcV1.PropertyDescriptor
            {
                PropertyId =
                    descriptor.Id.Value,
                DisplayName =
                    descriptor.DisplayName,
                AccessMode =
                    MapAccessMode(
                        descriptor.AccessMode),
                Data =
                    data
            };

        result.PathSegments.Add(
            descriptor.Path.Segments);

        if (descriptor.Description is not null)
        {
            result.Description =
                descriptor.Description;
        }

        return result;
    }

    private static GrpcV1.PropertyAccessMode MapAccessMode(
        DomainProperties.PropertyAccessMode accessMode)
    {
        return accessMode switch
        {
            DomainProperties.PropertyAccessMode.None =>
                GrpcV1.PropertyAccessMode.None,
            DomainProperties.PropertyAccessMode.Read =>
                GrpcV1.PropertyAccessMode.Read,
            DomainProperties.PropertyAccessMode.Write =>
                GrpcV1.PropertyAccessMode.Write,
            DomainProperties.PropertyAccessMode.ReadWrite =>
                GrpcV1.PropertyAccessMode.ReadWrite,
            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(accessMode),
                    accessMode,
                    "The Property access mode is not supported.")
        };
    }
}
