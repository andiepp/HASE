using DomainCommands = global::Hase.Core.Domain.Commands;
using DomainEvents = global::Hase.Core.Domain.Events;
using DomainInstruments = global::Hase.Core.Domain.Instruments;
using DomainProperties = global::Hase.Core.Domain.Properties;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter;

/// <summary>
/// Maps instrument descriptors to the version 1 remote contract.
/// </summary>
public sealed class InstrumentDescriptorMapper
    : IInstrumentDescriptorMapper
{
    private readonly IPropertyDescriptorMapper propertyDescriptorMapper;
    private readonly ICommandDescriptorMapper commandDescriptorMapper;
    private readonly IEventDescriptorMapper eventDescriptorMapper;

    /// <summary>
    /// Initializes the mapper.
    /// </summary>
    public InstrumentDescriptorMapper(
        IPropertyDescriptorMapper propertyDescriptorMapper,
        ICommandDescriptorMapper commandDescriptorMapper,
        IEventDescriptorMapper eventDescriptorMapper)
    {
        this.propertyDescriptorMapper =
            propertyDescriptorMapper
            ?? throw new ArgumentNullException(
                nameof(propertyDescriptorMapper));

        this.commandDescriptorMapper =
            commandDescriptorMapper
            ?? throw new ArgumentNullException(
                nameof(commandDescriptorMapper));

        this.eventDescriptorMapper =
            eventDescriptorMapper
            ?? throw new ArgumentNullException(
                nameof(eventDescriptorMapper));
    }

    /// <inheritdoc />
    public GrpcV1.InstrumentDescriptor Map(
        DomainInstruments.InstrumentDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(
            descriptor);

        var result =
            new GrpcV1.InstrumentDescriptor
            {
                InstrumentId =
                    descriptor.Id.Value,
                Name =
                    descriptor.Name,
                Kind =
                    descriptor.Kind.Name
            };

        MapMetadata(
            descriptor.Metadata,
            result);

        if (descriptor.Presentation is not null)
        {
            result.Presentation =
                MapPresentation(
                    descriptor.Presentation);
        }

        foreach (DomainProperties.PropertyDescriptor property in
                 descriptor.Interface.Properties)
        {
            result.Properties.Add(
                propertyDescriptorMapper.Map(
                    property)
                ?? throw new InvalidOperationException(
                    "The Property descriptor mapper returned null."));
        }

        foreach (DomainCommands.CommandDescriptor command in
                 descriptor.Interface.Commands)
        {
            result.Commands.Add(
                commandDescriptorMapper.Map(
                    command)
                ?? throw new InvalidOperationException(
                    "The Command descriptor mapper returned null."));
        }

        foreach (DomainEvents.EventDescriptor eventDescriptor in
                 descriptor.Interface.Events)
        {
            result.Events.Add(
                eventDescriptorMapper.Map(
                    eventDescriptor)
                ?? throw new InvalidOperationException(
                    "The Event descriptor mapper returned null."));
        }

        return result;
    }

    private static GrpcV1.InstrumentPresentation MapPresentation(
        DomainInstruments.InstrumentPresentation presentation)
    {
        var result =
            new GrpcV1.InstrumentPresentation();

        if (presentation.PanelId is not null)
        {
            result.PanelId =
                presentation.PanelId;
        }

        return result;
    }

    private static void MapMetadata(
        DomainInstruments.InstrumentMetadata source,
        GrpcV1.InstrumentDescriptor destination)
    {
        if (source.Manufacturer is not null)
        {
            destination.Manufacturer =
                source.Manufacturer;
        }

        if (source.Model is not null)
        {
            destination.Model =
                source.Model;
        }

        if (source.SerialNumber is not null)
        {
            destination.SerialNumber =
                source.SerialNumber;
        }

        if (source.FirmwareVersion is not null)
        {
            destination.FirmwareVersion =
                source.FirmwareVersion;
        }

        if (source.HardwareRevision is not null)
        {
            destination.HardwareRevision =
                source.HardwareRevision;
        }

        if (source.Description is not null)
        {
            destination.Description =
                source.Description;
        }
    }
}
