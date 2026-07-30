namespace Hase.DesktopHost;

/// <summary>
/// Represents one immutable diagnostic detail prepared for presentation.
/// </summary>
public sealed record DesktopRuntimeDiagnosticDetail(
    string Key,
    string Value);
