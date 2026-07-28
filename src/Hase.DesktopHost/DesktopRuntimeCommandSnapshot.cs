using Hase.Runtime.Northbound;

namespace Hase.DesktopHost;

public sealed record DesktopRuntimeCommandSnapshot(
    RuntimeHostCommandTarget Target,
    string Path,
    string DisplayName,
    string? Description,
    bool IsEndpointReady);
