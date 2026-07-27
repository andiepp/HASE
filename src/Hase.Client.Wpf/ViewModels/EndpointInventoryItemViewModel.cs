namespace Hase.Client.Wpf.ViewModels;

public sealed record EndpointInventoryItemViewModel(
    RemoteEndpointAttachmentKey Key,
    string EndpointId,
    string AttachmentGeneration,
    string DisplayName,
    string ConnectionState,
    bool IsReady,
    bool IsStale,
    IReadOnlyList<InstrumentInventoryItemViewModel> Instruments);
