using System.Globalization;
using Hase.Client.Wpf.ViewModels;
using Hase.Core.Domain.Data;

namespace Hase.Client.Wpf.Services;

public static class RuntimeHostInventoryProjector
{
    public static IReadOnlyList<EndpointInventoryItemViewModel> Project(
        RemoteObservationState state)
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
                                                        property))
                                            .ToArray()))
                            .ToArray()))
            .ToArray()
            ?? [];
    }

    private static PropertyInventoryItemViewModel ProjectProperty(
        RemoteObservationState state,
        RemoteEndpointAttachmentSnapshot attachment,
        Hase.Core.Domain.Identity.InstrumentId instrumentId,
        Hase.Core.Domain.Properties.PropertyDescriptor property)
    {
        var target =
            new RemotePropertyTarget(
                attachment.Key,
                instrumentId,
                property.Id);
        state.PropertyValues.TryGetValue(
            target,
            out RemotePropertyValue? cached);

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
            cached?.Quality.ToString());
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
            _ =>
                "No cached value"
        };
}
