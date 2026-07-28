namespace Hase.DesktopHost;

public sealed record DesktopRuntimeEndpointSnapshot(
    string EndpointId,
    string DisplayName,
    string ConnectionState,
    string AttachmentGeneration);
