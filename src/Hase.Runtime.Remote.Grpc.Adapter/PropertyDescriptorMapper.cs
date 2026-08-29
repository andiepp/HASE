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
    private readonly IUnitMapper unitMapper;

    /// <summary>
    /// Initializes the mapper.
    /// </summary>
    /// <summary>
    /// Initializes the mapper with a self-composed unit mapper.
    /// </summary>
    public PropertyDescriptorMapper(
        IDataDescriptorMapper dataDescriptorMapper)
        : this(
            dataDescriptorMapper,
            new UnitMapper(
                new QuantityMapper()))
    {
    }

    /// <summary>
    /// Initializes the mapper.
    /// </summary>
    public PropertyDescriptorMapper(
        IDataDescriptorMapper dataDescriptorMapper,
        IUnitMapper unitMapper)
    {
        this.dataDescriptorMapper =
            dataDescriptorMapper
            ?? throw new ArgumentNullException(
                nameof(dataDescriptorMapper));

        this.unitMapper =
            unitMapper
            ?? throw new ArgumentNullException(
                nameof(unitMapper));
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

        if (descriptor.Presentation is not null)
        {
            result.Presentation =
                MapPresentation(
                    descriptor.Presentation);
        }

        return result;
    }

    private GrpcV1.PropertyPresentation MapPresentation(
        DomainProperties.PropertyPresentation presentation)
    {
        var result =
            new GrpcV1.PropertyPresentation();

        if (presentation.GroupId is not null)
        {
            result.GroupId =
                presentation.GroupId;
        }

        if (presentation.Abscissa is not null)
        {
            result.Abscissa =
                new GrpcV1.QuantityValue
                {
                    Value =
                        presentation.Abscissa.Value,
                    Unit =
                        unitMapper.Map(
                            presentation.Abscissa.Unit)
                        ?? throw new InvalidOperationException(
                            "The unit mapper returned null.")
                };
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
