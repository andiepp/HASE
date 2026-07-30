using System.Collections.ObjectModel;
using System.Globalization;
using Hase.Runtime.Diagnostics;

namespace Hase.DesktopHost;

/// <summary>
/// Projects runtime diagnostic records into deterministic, UI-neutral Desktop
/// presentation entries.
/// </summary>
public static class DesktopRuntimeDiagnosticEntryProjector
{
    public static DesktopRuntimeDiagnosticEntry Project(
        RuntimeDiagnosticRecord record)
    {
        ArgumentNullException.ThrowIfNull(
            record);

        RuntimeDiagnosticByteSnapshot? byteSnapshot =
            record.ByteSnapshot;

        int originalByteCount =
            byteSnapshot?.OriginalByteCount
            ?? 0;

        int capturedByteCount =
            byteSnapshot?.CapturedByteCount
            ?? 0;

        bool isTruncated =
            byteSnapshot?.IsTruncated
            ?? false;

        return new DesktopRuntimeDiagnosticEntry(
            record.Sequence,
            record.TimestampUtc.ToUniversalTime(),
            record.TimestampUtc
                .ToUniversalTime()
                .ToString(
                    "O",
                    CultureInfo.InvariantCulture),
            record.Level,
            record.Category,
            record.EventName,
            record.Severity,
            record.EndpointId
                ?? string.Empty,
            record.AttachmentGeneration?.ToString(
                "D")
                ?? string.Empty,
            record.Direction?.ToString()
                ?? string.Empty,
            record.OperationId?.ToString(
                "D")
                ?? string.Empty,
            record.Duration?.ToString(
                "c",
                CultureInfo.InvariantCulture)
                ?? string.Empty,
            record.Outcome?.ToString()
                ?? string.Empty,
            ProjectDetails(
                record.Details),
            byteSnapshot is not null,
            originalByteCount,
            capturedByteCount,
            isTruncated,
            FormatByteSummary(
                byteSnapshot),
            byteSnapshot is null
                ? string.Empty
                : Convert.ToHexString(
                    byteSnapshot.ToArray()));
    }

    private static IReadOnlyList<DesktopRuntimeDiagnosticDetail>
        ProjectDetails(
            IReadOnlyDictionary<string, string> details)
    {
        DesktopRuntimeDiagnosticDetail[] projected =
            details
                .OrderBy(
                    detail =>
                        detail.Key,
                    StringComparer.Ordinal)
                .Select(
                    detail =>
                        new DesktopRuntimeDiagnosticDetail(
                            detail.Key,
                            detail.Value))
                .ToArray();

        return new ReadOnlyCollection<DesktopRuntimeDiagnosticDetail>(
            projected);
    }

    private static string FormatByteSummary(
        RuntimeDiagnosticByteSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return string.Empty;
        }

        string counts =
            string.Format(
                CultureInfo.InvariantCulture,
                "{0}/{1} bytes",
                snapshot.CapturedByteCount,
                snapshot.OriginalByteCount);

        return snapshot.IsTruncated
            ? $"{counts} (truncated)"
            : counts;
    }
}
