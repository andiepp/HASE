using Hase.Client.Wpf.ViewModels;

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
                                        instrument.Kind.Name))
                            .ToArray()))
            .ToArray()
            ?? [];
    }
}
