namespace Hase.Client.Wpf.ViewModels;

public sealed record EndpointInventoryItemViewModel(
    RemoteEndpointAttachmentKey Key,
    string EndpointId,
    string AttachmentGeneration,
    string DisplayName,
    string ConnectionState,
    IReadOnlyList<InstrumentInventoryItemViewModel> Instruments);
