namespace Hase.DesktopHost;

public sealed record DesktopRuntimeCommandSnapshot(
    string Path,
    string DisplayName,
    string? Description);
