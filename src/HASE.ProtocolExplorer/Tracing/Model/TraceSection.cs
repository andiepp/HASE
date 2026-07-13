namespace Hase.ProtocolExplorer.Tracing.Model;

internal sealed record TraceSection(
    string Title,
    IReadOnlyList<string> Lines);