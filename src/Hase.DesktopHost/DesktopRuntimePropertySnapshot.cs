namespace Hase.DesktopHost;

public sealed record DesktopRuntimePropertySnapshot(
    string PropertyId,
    string DisplayName,
    string Path,
    string Access,
    string Value,
    string Quality,
    string TimestampUtc,
    bool IsKnown);
