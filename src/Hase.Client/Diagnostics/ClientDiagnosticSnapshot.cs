namespace Hase.Client.Diagnostics;

/// <summary>
/// Captures retained client diagnostics and capacity evictions atomically.
/// </summary>
public sealed record ClientDiagnosticSnapshot(
    IReadOnlyList<ClientDiagnosticRecord> Records,
    long EvictedRecordCount);
