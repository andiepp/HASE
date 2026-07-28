namespace Hase.DesktopHost;

public sealed record DesktopRuntimeEventSnapshot(
    string Path,
    string DisplayName,
    string? Description);
