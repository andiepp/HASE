namespace Hase.DesktopHost.App.ViewModels;

public sealed record DesktopRuntimeEndpointViewModel(
    string EndpointId,
    string DisplayName,
    string ConnectionState,
    string AttachmentGeneration);
