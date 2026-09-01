using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Events;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using GrpcV1 = Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Client.Grpc;

/// <summary>
/// Maps version 1 protobuf observation messages into transport-independent
/// client contracts.
/// </summary>
public sealed class RuntimeHostGrpcObservationMapper
{
    /// <summary>
    /// Maps and validates the mandatory initial observation snapshot.
    /// </summary>
    public RemoteObservationInitialSnapshot MapInitialSnapshot(
        GrpcV1.ObservationInitialSnapshot initialSnapshot)
    {
        ArgumentNullException.ThrowIfNull(
            initialSnapshot);

        GrpcV1.GetSnapshotResponse snapshot =
            initialSnapshot.Snapshot
            ?? throw Invalid(
                "The observation initial snapshot has no runtime-host "
                + "snapshot.");
        GrpcV1.RuntimeHostApiVersion apiVersion =
            snapshot.ApiVersion
            ?? throw Invalid(
                "The runtime-host snapshot has no API version.");

        if (apiVersion.Major != 1)
        {
            throw new NotSupportedException(
                $"Runtime-host API major version {apiVersion.Major} is not "
                + "supported.");
        }

        return new RemoteObservationInitialSnapshot(
            new RemoteRuntimeHostSnapshot(
                new RemoteRuntimeHostId(
                    RequireText(
                        snapshot.RuntimeHostId,
                        "runtime-host ID")),
                new RuntimeHostClientApiVersion(
                    checked(
                        (ushort)apiVersion.Major),
                    checked(
                        (ushort)apiVersion.Minor)),
                snapshot.Endpoints.Select(
                    MapEndpoint)),
            new RemoteObservationSequence(
                initialSnapshot.SnapshotSequence));
    }

    /// <summary>
    /// Maps and validates one later observation.
    /// </summary>
    public RemoteRuntimeHostObservation MapObservation(
        GrpcV1.RuntimeHostObservation observation)
    {
        ArgumentNullException.ThrowIfNull(
            observation);

        var attachment =
            new RemoteEndpointAttachmentKey(
                new EndpointId(
                    RequireText(
                        observation.EndpointId,
                        "observation endpoint ID")),
                MapGeneration(
                    observation.AttachmentGeneration));

        RemoteObservationPayload payload =
            observation.PayloadCase switch
            {
                GrpcV1.RuntimeHostObservation.PayloadOneofCase
                    .AttachmentPublished =>
                    MapAttachmentPublished(
                        observation,
                        attachment),

                GrpcV1.RuntimeHostObservation.PayloadOneofCase
                    .AttachmentEnded =>
                    MapAttachmentEnded(
                        observation),

                GrpcV1.RuntimeHostObservation.PayloadOneofCase
                    .ConnectionStatusChanged =>
                    MapConnectionStatusChanged(
                        observation),

                GrpcV1.RuntimeHostObservation.PayloadOneofCase
                    .PropertyValueChanged =>
                    MapPropertyValueChanged(
                        observation),

                GrpcV1.RuntimeHostObservation.PayloadOneofCase
                    .EventOccurred =>
                    MapEventOccurred(
                        observation),

                _ =>
                    throw Invalid(
                        "The runtime-host observation has no supported "
                        + "payload.")
            };

        return new RemoteRuntimeHostObservation(
            new RemoteObservationSequence(
                observation.Sequence),
            attachment,
            payload);
    }

    private static RemoteObservationPayload MapAttachmentPublished(
        GrpcV1.RuntimeHostObservation observation,
        RemoteEndpointAttachmentKey attachment)
    {
        RequireKind(
            observation,
            GrpcV1.RuntimeHostObservationKind.AttachmentPublished);

        GrpcV1.PublishedRuntimeEndpointSnapshot endpoint =
            observation.AttachmentPublished.Endpoint
            ?? throw Invalid(
                "The attachment-published observation has no endpoint "
                + "snapshot.");
        RemoteEndpointAttachmentSnapshot mappedEndpoint =
            MapEndpoint(
                endpoint);

        if (mappedEndpoint.Key != attachment)
        {
            throw Invalid(
                "The published endpoint attachment does not match its "
                + "observation envelope.");
        }

        return new RemoteAttachmentPublishedObservationPayload(
            mappedEndpoint);
    }

    private static RemoteObservationPayload MapAttachmentEnded(
        GrpcV1.RuntimeHostObservation observation)
    {
        RequireKind(
            observation,
            GrpcV1.RuntimeHostObservationKind.AttachmentEnded);

        return new RemoteAttachmentEndedObservationPayload(
            MapRequiredTimestamp(
                observation.AttachmentEnded.EndedAtUtc,
                "attachment-ended time"));
    }

    private static RemoteObservationPayload MapConnectionStatusChanged(
        GrpcV1.RuntimeHostObservation observation)
    {
        RequireKind(
            observation,
            GrpcV1.RuntimeHostObservationKind.ConnectionStatusChanged);

        return new RemoteConnectionStatusChangedObservationPayload(
            MapConnectionStatus(
                observation.ConnectionStatusChanged.PreviousStatus),
            MapConnectionStatus(
                observation.ConnectionStatusChanged.CurrentStatus));
    }

    private static RemoteObservationPayload MapPropertyValueChanged(
        GrpcV1.RuntimeHostObservation observation)
    {
        RequireKind(
            observation,
            GrpcV1.RuntimeHostObservationKind.PropertyValueChanged);

        GrpcV1.PropertyValueChangedObservation payload =
            observation.PropertyValueChanged;

        return new RemotePropertyValueChangedObservationPayload(
            new InstrumentId(
                RequireText(
                    payload.InstrumentId,
                    "Property observation instrument ID")),
            new PropertyId(
                RequireText(
                    payload.PropertyId,
                    "Property observation Property ID")),
            payload.PreviousValue is null
                ? null
                : MapPropertyValue(
                    payload.PreviousValue),
            MapPropertyValue(
                payload.CurrentValue));
    }

    private static RemoteObservationPayload MapEventOccurred(
        GrpcV1.RuntimeHostObservation observation)
    {
        RequireKind(
            observation,
            GrpcV1.RuntimeHostObservationKind.EventOccurred);

        GrpcV1.EventOccurredObservation payload =
            observation.EventOccurred;

        return new RemoteEventOccurredObservationPayload(
            new InstrumentId(
                RequireText(
                    payload.InstrumentId,
                    "Event observation instrument ID")),
            MapPath(
                payload.EventPathSegments,
                "Event observation path"),
            MapRequiredTimestamp(
                payload.OccurredAtUtc,
                "Event occurrence time"),
            payload.Value is null
                ? null
                : MapRemoteValue(
                    payload.Value));
    }

    private static RemoteEndpointAttachmentSnapshot MapEndpoint(
        GrpcV1.PublishedRuntimeEndpointSnapshot endpoint)
    {
        ArgumentNullException.ThrowIfNull(
            endpoint);

        string endpointId =
            RequireText(
                endpoint.EndpointId,
                "published endpoint ID");
        EndpointDescriptor descriptor =
            MapEndpointDescriptor(
                endpoint.Descriptor_);

        if (descriptor.Id.Value != endpointId)
        {
            throw Invalid(
                "The published endpoint identity does not match its "
                + "descriptor.");
        }

        return new RemoteEndpointAttachmentSnapshot(
            MapGeneration(
                endpoint.AttachmentGeneration),
            descriptor,
            MapConnectionStatus(
                endpoint.ConnectionStatus));
    }

    private static EndpointDescriptor MapEndpointDescriptor(
        GrpcV1.EndpointDescriptor? descriptor)
    {
        if (descriptor is null)
        {
            throw Invalid(
                "The published endpoint has no descriptor.");
        }

        return new EndpointDescriptor(
            new EndpointId(
                RequireText(
                    descriptor.EndpointId,
                    "descriptor endpoint ID")),
            descriptor.Instruments.Select(
                MapInstrumentDescriptor))
        {
            Metadata =
                new EndpointMetadata
                {
                    DisplayName =
                        OptionalText(
                            descriptor.HasDisplayName,
                            descriptor.DisplayName),
                    Description =
                        OptionalText(
                            descriptor.HasDescription,
                            descriptor.Description)
                }
        };
    }

    private static InstrumentDescriptor MapInstrumentDescriptor(
        GrpcV1.InstrumentDescriptor descriptor)
    {
        var mapped =
            new InstrumentDescriptor(
                new InstrumentId(
                    RequireText(
                        descriptor.InstrumentId,
                        "instrument ID")),
                RequireText(
                    descriptor.Name,
                    "instrument name"),
                new InstrumentKind(
                    RequireText(
                        descriptor.Kind,
                        "instrument kind")))
            {
                Metadata =
                    new InstrumentMetadata
                    {
                        Manufacturer =
                            OptionalText(
                                descriptor.HasManufacturer,
                                descriptor.Manufacturer),
                        Model =
                            OptionalText(
                                descriptor.HasModel,
                                descriptor.Model),
                        SerialNumber =
                            OptionalText(
                                descriptor.HasSerialNumber,
                                descriptor.SerialNumber),
                        FirmwareVersion =
                            OptionalText(
                                descriptor.HasFirmwareVersion,
                                descriptor.FirmwareVersion),
                        HardwareRevision =
                            OptionalText(
                                descriptor.HasHardwareRevision,
                                descriptor.HardwareRevision),
                        Description =
                            OptionalText(
                                descriptor.HasDescription,
                                descriptor.Description)
                    },
                Interface =
                    new InstrumentInterface(
                        descriptor.Properties.Select(
                            MapPropertyDescriptor),
                        descriptor.Commands.Select(
                            MapCommandDescriptor),
                        descriptor.Events.Select(
                            MapEventDescriptor)),
                Presentation =
                    MapInstrumentPresentation(
                        descriptor.Presentation)
            };

        return mapped;
    }

    private static InstrumentPresentation? MapInstrumentPresentation(
        GrpcV1.InstrumentPresentation? presentation)
    {
        if (presentation is null)
        {
            return null;
        }

        return new InstrumentPresentation
        {
            PanelId =
                OptionalText(
                    presentation.HasPanelId,
                    presentation.PanelId)
        };
    }

    private static PropertyDescriptor MapPropertyDescriptor(
        GrpcV1.PropertyDescriptor descriptor)
    {
        return new PropertyDescriptor(
            new PropertyId(
                RequireText(
                    descriptor.PropertyId,
                    "Property ID")),
            MapPath(
                descriptor.PathSegments,
                "Property path"),
            RequireText(
                descriptor.DisplayName,
                "Property display name"),
            MapDataDescriptor(
                descriptor.Data))
        {
            Description =
                OptionalText(
                    descriptor.HasDescription,
                    descriptor.Description),
            AccessMode =
                descriptor.AccessMode switch
                {
                    GrpcV1.PropertyAccessMode.None =>
                        PropertyAccessMode.None,
                    GrpcV1.PropertyAccessMode.Read =>
                        PropertyAccessMode.Read,
                    GrpcV1.PropertyAccessMode.Write =>
                        PropertyAccessMode.Write,
                    GrpcV1.PropertyAccessMode.ReadWrite =>
                        PropertyAccessMode.ReadWrite,
                    _ =>
                        throw Invalid(
                            "The Property descriptor has an unsupported "
                            + "access mode.")
                },
            Presentation =
                MapPresentation(
                    descriptor.Presentation)
        };
    }

    private static PropertyPresentation? MapPresentation(
        GrpcV1.PropertyPresentation? presentation)
    {
        if (presentation is null)
        {
            return null;
        }

        return new PropertyPresentation
        {
            GroupId =
                OptionalText(
                    presentation.HasGroupId,
                    presentation.GroupId),
            Abscissa =
                presentation.Abscissa is null
                    ? null
                    : new QuantityValue(
                        presentation.Abscissa.Value,
                        MapUnit(
                            presentation.Abscissa.Unit))
        };
    }

    private static DataDescriptor MapDataDescriptor(
        GrpcV1.DataDescriptor? descriptor)
    {
        if (descriptor is null)
        {
            throw Invalid(
                "The Property descriptor has no data descriptor.");
        }

        return descriptor.KindCase switch
        {
            GrpcV1.DataDescriptor.KindOneofCase.Numeric =>
                MapNumericDataDescriptor(
                    descriptor.Numeric),

            GrpcV1.DataDescriptor.KindOneofCase.BooleanDescriptor =>
                new BooleanDataDescriptor(),

            GrpcV1.DataDescriptor.KindOneofCase.StringDescriptor =>
                new StringDataDescriptor(),

            GrpcV1.DataDescriptor.KindOneofCase.ByteArrayDescriptor =>
                new ByteArrayDataDescriptor(),

            _ =>
                throw Invalid(
                    "The Property data descriptor has no supported kind.")
        };
    }

    private static NumericDataDescriptor MapNumericDataDescriptor(
        GrpcV1.NumericDataDescriptor descriptor)
    {
        Quantity quantity =
            MapQuantity(
                descriptor.Quantity);
        Unit unit =
            MapUnit(
                descriptor.NativeUnit);

        if (unit.Quantity != quantity)
        {
            throw Invalid(
                "The native unit quantity does not match the numeric "
                + "descriptor quantity.");
        }

        return new NumericDataDescriptor(
            quantity,
            unit,
            descriptor.Range is null
                ? null
                : new ValueRange(
                    descriptor.Range.Minimum,
                    descriptor.Range.Maximum),
            descriptor.Resolution is null
                ? null
                : new Resolution(
                    descriptor.Resolution.Value));
    }

    private static Quantity MapQuantity(
        GrpcV1.Quantity? quantity)
    {
        if (quantity is null)
        {
            throw Invalid(
                "The numeric descriptor has no quantity.");
        }

        return new Quantity(
            RequireText(
                quantity.Id,
                "quantity ID"),
            RequireText(
                quantity.DisplayName,
                "quantity display name"));
    }

    private static Unit MapUnit(
        GrpcV1.Unit? unit)
    {
        if (unit is null)
        {
            throw Invalid(
                "The numeric descriptor has no native unit.");
        }

        return new Unit(
            RequireText(
                unit.Id,
                "unit ID"),
            RequireText(
                unit.DisplayName,
                "unit display name"),
            RequireText(
                unit.Symbol,
                "unit symbol"),
            MapQuantity(
                unit.Quantity));
    }

    private static CommandDescriptor MapCommandDescriptor(
        GrpcV1.CommandDescriptor descriptor)
    {
        DescriptorPath path =
            MapPath(
                descriptor.PathSegments,
                "Command path");
        string displayName =
            RequireText(
                descriptor.DisplayName,
                "Command display name");
        string? description =
            OptionalText(
                descriptor.HasDescription,
                descriptor.Description);

        CommandPresentation? presentation =
            MapCommandPresentation(
                descriptor.Presentation);

        if (descriptor.Argument is null)
        {
            return new CommandDescriptor(
                path,
                displayName)
            {
                Description =
                    description,
                Presentation =
                    presentation,
                RequiresExplicitConfirmation =
                    descriptor.RequiresExplicitConfirmation
            };
        }

        return new CommandDescriptor(
            path,
            displayName,
            new CommandArgumentDescriptor(
                RequireText(
                    descriptor.Argument.DisplayName,
                    "Command argument display name"),
                MapDataDescriptor(
                    descriptor.Argument.Data))
            {
                Description =
                    OptionalText(
                        descriptor.Argument.HasDescription,
                        descriptor.Argument.Description)
            })
        {
            Description =
                description,
            Presentation =
                presentation,
            RequiresExplicitConfirmation =
                descriptor.RequiresExplicitConfirmation
        };
    }

    private static CommandPresentation? MapCommandPresentation(
        GrpcV1.CommandPresentation? presentation)
    {
        if (presentation is null)
        {
            return null;
        }

        return new CommandPresentation
        {
            ShortLabel =
                OptionalText(
                    presentation.HasShortLabel,
                    presentation.ShortLabel),
            SelectionGroupId =
                OptionalText(
                    presentation.HasSelectionGroupId,
                    presentation.SelectionGroupId),
            SelectionStatePath =
                presentation.SelectionStatePathSegments.Count == 0
                    ? null
                    : MapPath(
                        presentation.SelectionStatePathSegments,
                        "Command selection state path"),
            SelectionValue =
                OptionalText(
                    presentation.HasSelectionValue,
                    presentation.SelectionValue)
        };
    }

    private static EventDescriptor MapEventDescriptor(
        GrpcV1.EventDescriptor descriptor)
    {
        DescriptorPath path =
            MapPath(
                descriptor.PathSegments,
                "Event path");
        string displayName =
            RequireText(
                descriptor.DisplayName,
                "Event display name");
        string? description =
            OptionalText(
                descriptor.HasDescription,
                descriptor.Description);

        if (descriptor.Payload is null)
        {
            return new EventDescriptor(
                path,
                displayName)
            {
                Description = description
            };
        }

        return new EventDescriptor(
            path,
            displayName)
        {
            Description = description,
            Payload =
                new EventPayloadDescriptor(
                RequireText(
                    descriptor.Payload.DisplayName,
                    "Event payload display name"),
                MapDataDescriptor(
                    descriptor.Payload.Data))
                {
                    Description =
                        OptionalText(
                            descriptor.Payload.HasDescription,
                            descriptor.Payload.Description)
                }
        };
    }

    private static RemoteEndpointConnectionStatus MapConnectionStatus(
        GrpcV1.EndpointConnectionStatus? status)
    {
        if (status is null)
        {
            throw Invalid(
                "The endpoint connection status is absent.");
        }

        RemoteEndpointConnectionState state =
            status.State switch
            {
                GrpcV1.EndpointConnectionState.Disconnected =>
                    RemoteEndpointConnectionState.Disconnected,
                GrpcV1.EndpointConnectionState.Connecting =>
                    RemoteEndpointConnectionState.Connecting,
                GrpcV1.EndpointConnectionState.Synchronizing =>
                    RemoteEndpointConnectionState.Synchronizing,
                GrpcV1.EndpointConnectionState.Ready =>
                    RemoteEndpointConnectionState.Ready,
                GrpcV1.EndpointConnectionState.Reconnecting =>
                    RemoteEndpointConnectionState.Reconnecting,
                GrpcV1.EndpointConnectionState.Faulted =>
                    RemoteEndpointConnectionState.Faulted,
                _ =>
                    throw Invalid(
                        "The endpoint connection status has an unsupported "
                        + "state.")
            };

        return new RemoteEndpointConnectionStatus(
            state,
            status.ChangedAtUtc is null
                ? null
                : MapRequiredTimestamp(
                    status.ChangedAtUtc,
                    "connection-status change time"),
            OptionalText(
                status.HasDetail,
                status.Detail));
    }

    private static RemotePropertyValue MapPropertyValue(
        GrpcV1.PropertyValue? propertyValue)
    {
        if (propertyValue is null)
        {
            throw Invalid(
                "The required Property value is absent.");
        }

        return new RemotePropertyValue(
            propertyValue.Value is null
                ? null
                : MapRemoteValue(
                    propertyValue.Value),
            MapRequiredTimestamp(
                propertyValue.TimestampUtc,
                "Property-value timestamp"),
            propertyValue.Quality switch
            {
                GrpcV1.PropertyQuality.Good =>
                    RemotePropertyQuality.Good,
                GrpcV1.PropertyQuality.Uncertain =>
                    RemotePropertyQuality.Uncertain,
                GrpcV1.PropertyQuality.Bad =>
                    RemotePropertyQuality.Bad,
                _ =>
                    throw Invalid(
                        "The Property value has an unsupported quality.")
            });
    }

    private static RemoteValue MapRemoteValue(
        GrpcV1.RemoteValue value)
    {
        return value.KindCase switch
        {
            GrpcV1.RemoteValue.KindOneofCase.BooleanValue =>
                RemoteValue.FromBoolean(
                    value.BooleanValue),
            GrpcV1.RemoteValue.KindOneofCase.StringValue =>
                RemoteValue.FromString(
                    value.StringValue),
            GrpcV1.RemoteValue.KindOneofCase.NumericValue =>
                RemoteValue.FromNumeric(
                    value.NumericValue),
            GrpcV1.RemoteValue.KindOneofCase.ByteArrayValue =>
                RemoteValue.FromByteArray(
                    new ByteArrayValue(
                        value.ByteArrayValue.ToByteArray())),
            _ =>
                throw Invalid(
                    "The remote value has no supported kind.")
        };
    }

    private static DescriptorPath MapPath(
        IEnumerable<string> segments,
        string name)
    {
        string[] snapshot =
            segments.ToArray();

        if (snapshot.Length == 0
            || snapshot.Any(
                string.IsNullOrWhiteSpace))
        {
            throw Invalid(
                $"The {name} must contain non-empty segments.");
        }

        return new DescriptorPath(
            snapshot);
    }

    private static RemoteEndpointAttachmentGeneration MapGeneration(
        string value)
    {
        if (!Guid.TryParseExact(
                value,
                "D",
                out Guid generation)
            || generation == Guid.Empty)
        {
            throw Invalid(
                "The attachment generation is not a non-empty canonical "
                + "GUID.");
        }

        return new RemoteEndpointAttachmentGeneration(
            generation);
    }

    private static DateTimeOffset MapRequiredTimestamp(
        Google.Protobuf.WellKnownTypes.Timestamp? timestamp,
        string name)
    {
        if (timestamp is null)
        {
            throw Invalid(
                $"The {name} is absent.");
        }

        try
        {
            return timestamp.ToDateTimeOffset();
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidDataException(
                $"The {name} is invalid.",
                exception);
        }
    }

    private static void RequireKind(
        GrpcV1.RuntimeHostObservation observation,
        GrpcV1.RuntimeHostObservationKind expected)
    {
        if (observation.Kind != expected)
        {
            throw Invalid(
                "The runtime-host observation kind does not match its "
                + "payload.");
        }
    }

    private static string RequireText(
        string value,
        string name)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            throw Invalid(
                $"The {name} is empty.");
        }

        return value.Trim();
    }

    private static string? OptionalText(
        bool hasValue,
        string value)
    {
        if (!hasValue)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(
                value)
            ? null
            : value.Trim();
    }

    private static InvalidDataException Invalid(
        string message)
    {
        return new InvalidDataException(
            message);
    }
}
