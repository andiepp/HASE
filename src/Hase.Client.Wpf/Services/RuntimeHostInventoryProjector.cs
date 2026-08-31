using System.Globalization;
using Hase.Client.Wpf.ViewModels;
using Hase.Core.Domain.Data;

namespace Hase.Client.Wpf.Services;

public static class RuntimeHostInventoryProjector
{
    public static IReadOnlyList<EndpointInventoryItemViewModel> Project(
        RemoteObservationState state,
        IReadOnlyDictionary<
            RemotePropertyTarget,
            RemotePropertyValue>? confirmedReads = null,
        IReadOnlyDictionary<
            RemotePropertyTarget,
            bool>? requestedBooleanValues = null,
        IReadOnlyDictionary<
            RemoteCommandTarget,
            string>? requestedCommandArgumentTexts = null,
        IReadOnlyDictionary<
            RemotePropertyTarget,
            string>? requestedPropertyValueTexts = null,
        IReadOnlySet<string>? availablePanelIds = null)
    {
        ArgumentNullException.ThrowIfNull(
            state);

        return state.Snapshot?.Attachments
            .Select(
                attachment =>
                    ProjectEndpoint(
                        state,
                        attachment,
                        confirmedReads,
                        requestedBooleanValues,
                        requestedCommandArgumentTexts,
                        requestedPropertyValueTexts,
                        availablePanelIds))
            .ToArray()
            ?? [];
    }

    private static EndpointInventoryItemViewModel ProjectEndpoint(
        RemoteObservationState state,
        RemoteEndpointAttachmentSnapshot attachment,
        IReadOnlyDictionary<
            RemotePropertyTarget,
            RemotePropertyValue>? confirmedReads,
        IReadOnlyDictionary<
            RemotePropertyTarget,
            bool>? requestedBooleanValues,
        IReadOnlyDictionary<
            RemoteCommandTarget,
            string>? requestedCommandArgumentTexts,
        IReadOnlyDictionary<
            RemotePropertyTarget,
            string>? requestedPropertyValueTexts,
        IReadOnlySet<string>? availablePanelIds)
    {
        InstrumentInventoryItemViewModel[] instruments =
            attachment.Descriptor.Instruments
                .Select(
                    instrument =>
                        new InstrumentInventoryItemViewModel(
                            instrument.Id.Value,
                            instrument.Name,
                            instrument.Kind.Name,
                            instrument.Interface.Properties
                                .Select(
                                    property =>
                                        ProjectProperty(
                                            state,
                                            attachment,
                                            instrument.Id,
                                            property,
                                            confirmedReads,
                                            requestedBooleanValues,
                                            requestedPropertyValueTexts))
                                .ToArray(),
                            instrument.Interface.Commands
                                .Select(
                                    command =>
                                        ProjectCommand(
                                            state,
                                            attachment,
                                            instrument.Id,
                                            instrument.Interface.Properties,
                                            command,
                                            confirmedReads,
                                            requestedCommandArgumentTexts))
                                .ToArray())
                        {
                            PanelId =
                                instrument.Presentation?.PanelId
                        })
                .ToArray();

        // A declared panel is offered only when this client hosts one by that
        // name; an unknown declaration presents exactly as no declaration.
        InstrumentInventoryItemViewModel? panelInstrument =
            availablePanelIds is null
                ? null
                : instruments.FirstOrDefault(
                    instrument =>
                        instrument.PanelId is not null
                        && availablePanelIds.Contains(instrument.PanelId));

        return new EndpointInventoryItemViewModel(
            attachment.Key,
            attachment.EndpointId.Value,
            attachment.Generation.ToString(),
            attachment.Descriptor.Metadata.DisplayName
                ?? attachment.EndpointId.Value,
            attachment.ConnectionStatus.State.ToString(),
            attachment.ConnectionStatus.State
                == RemoteEndpointConnectionState.Ready,
            attachment.ConnectionStatus.State
                != RemoteEndpointConnectionState.Ready,
            instruments)
        {
            PanelId =
                panelInstrument?.PanelId,
            PanelInstrumentId =
                panelInstrument?.InstrumentId
        };
    }

    private static CommandInventoryItemViewModel ProjectCommand(
        RemoteObservationState state,
        RemoteEndpointAttachmentSnapshot attachment,
        Hase.Core.Domain.Identity.InstrumentId instrumentId,
        IReadOnlyList<Hase.Core.Domain.Properties.PropertyDescriptor> properties,
        Hase.Core.Domain.Commands.CommandDescriptor command,
        IReadOnlyDictionary<
            RemotePropertyTarget,
            RemotePropertyValue>? confirmedReads,
        IReadOnlyDictionary<
            RemoteCommandTarget,
            string>? requestedCommandArgumentTexts)
    {
        var target =
            new RemoteCommandTarget(
                attachment.Key,
                instrumentId,
                command.Path);
        string requestedText =
            FindRequestedCommandArgumentText(
                requestedCommandArgumentTexts,
                target)
            ?? string.Empty;

        return new CommandInventoryItemViewModel(
            target,
            command.Path.ToString(),
            command.DisplayName,
            command.Description,
            attachment.ConnectionStatus.State
                == RemoteEndpointConnectionState.Ready)
        {
            Descriptor =
                command,
            AuthoritativeOperatingMode =
                FindAuthoritativeOperatingMode(
                    state,
                    attachment,
                    instrumentId,
                    properties,
                    confirmedReads),
            RequestedArgumentText =
                requestedText,
            RequestedBooleanArgument =
                command.Argument?.Data
                    is BooleanDataDescriptor
                && bool.TryParse(
                    requestedText,
                    out bool requestedBoolean)
                    ? requestedBoolean
                    : null
        };
    }

    private static string? FindAuthoritativeOperatingMode(
        RemoteObservationState state,
        RemoteEndpointAttachmentSnapshot attachment,
        Hase.Core.Domain.Identity.InstrumentId instrumentId,
        IReadOnlyList<Hase.Core.Domain.Properties.PropertyDescriptor> properties,
        IReadOnlyDictionary<
            RemotePropertyTarget,
            RemotePropertyValue>? confirmedReads)
    {
        if (attachment.ConnectionStatus.State
            != RemoteEndpointConnectionState.Ready)
        {
            return null;
        }

        Hase.Core.Domain.Properties.PropertyDescriptor? operatingMode =
            properties.SingleOrDefault(
                property =>
                    string.Equals(
                        property.Path.ToString(),
                        "Operating.Mode",
                        StringComparison.Ordinal));
        if (operatingMode is null)
        {
            return null;
        }

        var target = new RemotePropertyTarget(
            attachment.Key,
            instrumentId,
            operatingMode.Id);
        RemotePropertyValue? value = null;
        if (confirmedReads is not null)
        {
            confirmedReads.TryGetValue(
                target,
                out value);
        }

        if (value is null)
        {
            state.PropertyValues.TryGetValue(
                target,
                out value);
        }

        return value?.Quality == RemotePropertyQuality.Good
            ? value.Value?.StringValue
            : null;
    }

    private static string? FindRequestedCommandArgumentText(
        IReadOnlyDictionary<
            RemoteCommandTarget,
            string>? values,
        RemoteCommandTarget target)
    {
        if (values is null)
        {
            return null;
        }

        if (values.TryGetValue(
                target,
                out string? exact))
        {
            return exact;
        }

        foreach (KeyValuePair<
            RemoteCommandTarget,
            string> item in values)
        {
            if (string.Equals(
                    item.Key.InstrumentId.Value,
                    target.InstrumentId.Value,
                    StringComparison.Ordinal)
                && string.Equals(
                    item.Key.CommandPath.ToString(),
                    target.CommandPath.ToString(),
                    StringComparison.Ordinal))
            {
                return item.Value;
            }
        }

        return null;
    }

    private static PropertyInventoryItemViewModel ProjectProperty(
        RemoteObservationState state,
        RemoteEndpointAttachmentSnapshot attachment,
        Hase.Core.Domain.Identity.InstrumentId instrumentId,
        Hase.Core.Domain.Properties.PropertyDescriptor property,
        IReadOnlyDictionary<
            RemotePropertyTarget,
            RemotePropertyValue>? confirmedReads,
        IReadOnlyDictionary<
            RemotePropertyTarget,
            bool>? requestedBooleanValues,
        IReadOnlyDictionary<
            RemotePropertyTarget,
            string>? requestedPropertyValueTexts)
    {
        var target =
            new RemotePropertyTarget(
                attachment.Key,
                instrumentId,
                property.Id);
        state.PropertyValues.TryGetValue(
            target,
            out RemotePropertyValue? cached);
        if (confirmedReads is not null
            && confirmedReads.TryGetValue(
                target,
                out RemotePropertyValue? confirmed))
        {
            cached =
                confirmed;
        }

        bool endpointReady =
            attachment.ConnectionStatus.State
            == RemoteEndpointConnectionState.Ready;
        bool readable =
            property.AccessMode is
                Hase.Core.Domain.Properties.PropertyAccessMode.Read
                or Hase.Core.Domain.Properties.PropertyAccessMode.ReadWrite;
        bool writable =
            property.AccessMode.HasFlag(
                Hase.Core.Domain.Properties.PropertyAccessMode.Write);
        bool supportedWritable =
            writable
            && (property.Data is BooleanDataDescriptor
                || property.Data is NumericDataDescriptor
                || property.Data is StringDataDescriptor
                || property.Data is ByteArrayDataDescriptor);
        bool booleanWritable =
            supportedWritable
            && property.Data is BooleanDataDescriptor;
        bool requestedBooleanValue =
            booleanWritable
            && requestedBooleanValues is not null
            && requestedBooleanValues.TryGetValue(
                target,
                out bool requested)
                ? requested
                : cached?.Value?.BooleanValue
                    ?? false;
        string requestedValueText =
            requestedPropertyValueTexts is not null
            && requestedPropertyValueTexts.TryGetValue(
                target,
                out string? requestedText)
                ? requestedText
                : FormatEditableValue(
                    cached?.Value,
                    property.Data);

        return new PropertyInventoryItemViewModel(
            target,
            property.Id.Value,
            property.Path.ToString(),
            property.DisplayName,
            property.AccessMode.ToString(),
            property.Data switch
            {
                NumericDataDescriptor =>
                    "Numeric",
                BooleanDataDescriptor =>
                    "Boolean",
                StringDataDescriptor =>
                    "String",
                ByteArrayDataDescriptor =>
                    "ByteArray",
                _ =>
                    property.Data.GetType().Name
            },
            property.Data is NumericDataDescriptor numeric
                ? numeric.NativeUnit.Symbol
                : null,
            FormatValue(
                cached?.Value),
            cached?.TimestampUtc.ToString(
                "O",
                CultureInfo.InvariantCulture),
            cached?.Quality.ToString(),
            !endpointReady,
            readable,
            endpointReady
                && readable,
            booleanWritable,
            endpointReady
                && supportedWritable,
            property,
            supportedWritable
                ? property.Data is BooleanDataDescriptor
                    ? PropertyInputEditorKind.Boolean
                    : PropertyInputEditorKind.Text
                : PropertyInputEditorKind.None,
            requestedValueText)
        {
            RequestedBooleanValue =
                requestedBooleanValue
        };
    }

    private static string FormatValue(
        RemoteValue? value) =>
        value?.Kind switch
        {
            RemoteValueKind.Boolean =>
                value.BooleanValue!.Value
                    ? "True"
                    : "False",
            RemoteValueKind.String =>
                value.StringValue!,
            RemoteValueKind.Numeric =>
                value.NumericValue!.Value.ToString(
                    "G17",
                    CultureInfo.InvariantCulture),
            RemoteValueKind.ByteArray =>
                Convert.ToHexString(
                    value.ByteArrayValue!.AsSpan()),
            _ =>
                "No cached value"
        };

    private static string FormatEditableValue(
        RemoteValue? value,
        DataDescriptor descriptor)
    {
        if (value is null)
        {
            return string.Empty;
        }

        return descriptor switch
        {
            NumericDataDescriptor
                when value.Kind == RemoteValueKind.Numeric =>
                    value.NumericValue!.Value.ToString(
                        "G17",
                        CultureInfo.InvariantCulture),
            StringDataDescriptor
                when value.Kind == RemoteValueKind.String =>
                    value.StringValue!,
            ByteArrayDataDescriptor
                when value.Kind == RemoteValueKind.ByteArray =>
                    string.Join(
                        " ",
                        value.ByteArrayValue!
                            .ToArray()
                            .Select(
                                item =>
                                    item.ToString(
                                        "X2",
                                        CultureInfo.InvariantCulture))),
            _ =>
                string.Empty
        };
    }
}
