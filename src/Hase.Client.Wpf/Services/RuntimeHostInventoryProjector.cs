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
            string>? requestedPropertyValueTexts = null)
    {
        ArgumentNullException.ThrowIfNull(
            state);

        return state.Snapshot?.Attachments
            .Select(
                attachment =>
                    new EndpointInventoryItemViewModel(
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
                                                    new CommandInventoryItemViewModel(
                                                        new RemoteCommandTarget(
                                                            attachment.Key,
                                                            instrument.Id,
                                                            command.Path),
                                                        command.Path.ToString(),
                                                        command.DisplayName,
                                                        command.Description,
                                                        attachment.ConnectionStatus.State
                                                            == RemoteEndpointConnectionState.Ready)
                                                    {
                                                        RequiresArgument =
                                                            command.Argument
                                                            is not null,
                                                        ArgumentDisplayName =
                                                            command.Argument
                                                                ?.DisplayName,
                                                        ArgumentDescription =
                                                            command.Argument
                                                                ?.Description,
                                                        ArgumentDataType =
                                                            command.Argument
                                                            is null
                                                                ? null
                                                                : GetDataType(
                                                                    command.Argument.Data),
                                                        RequestedArgumentText =
                                                            requestedCommandArgumentTexts
                                                            is not null
                                                            && requestedCommandArgumentTexts
                                                                .TryGetValue(
                                                                    new RemoteCommandTarget(
                                                                        attachment.Key,
                                                                        instrument.Id,
                                                                        command.Path),
                                                                    out string? requestedText)
                                                                ? requestedText
                                                                : string.Empty
                                                    })
                                            .ToArray()))
                            .ToArray()))
            .ToArray()
            ?? [];
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

    private static string GetDataType(
        DataDescriptor descriptor) =>
        descriptor switch
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
                descriptor.GetType().Name
        };
}
