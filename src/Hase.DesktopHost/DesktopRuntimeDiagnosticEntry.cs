using Hase.Runtime.Diagnostics;

namespace Hase.DesktopHost;

/// <summary>
/// Represents one immutable, safely formatted Desktop Runtime Host diagnostic
/// entry.
/// </summary>
public sealed record DesktopRuntimeDiagnosticEntry(
    long Sequence,
    DateTimeOffset TimestampUtc,
    string TimestampUtcText,
    RuntimeDiagnosticLevel Level,
    RuntimeDiagnosticCategory Category,
    string EventName,
    RuntimeDiagnosticSeverity Severity,
    string EndpointId,
    string AttachmentGeneration,
    string Direction,
    string OperationId,
    string Duration,
    string Outcome,
    IReadOnlyList<DesktopRuntimeDiagnosticDetail> Details,
    bool HasByteSnapshot,
    int OriginalByteCount,
    int CapturedByteCount,
    bool IsByteSnapshotTruncated,
    string ByteSummary,
    string ByteHex);
