using Hase.Client.Diagnostics;
using Hase.Runtime.Diagnostics;

namespace Hase.Diagnostics.Export;

/// <summary>
/// Builds one export document from retained Runtime Host diagnostics.
/// The mapping carries exactly the captured, sanitized fields.
/// </summary>
public static class RuntimeHostDiagnosticExport
{
    public static DiagnosticExportDocument ToDocument(
        RuntimeDiagnosticLevel captureLevel,
        string? runtimeHostId,
        DateTimeOffset exportedAtUtc,
        IReadOnlyList<RuntimeDiagnosticRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var exportedRecords = new ExportedDiagnosticRecord[records.Count];
        for (int index = 0; index < records.Count; index++)
        {
            RuntimeDiagnosticRecord record = records[index]
                ?? throw new ArgumentException(
                    "A diagnostic record must not be null.",
                    nameof(records));

            exportedRecords[index] = new ExportedDiagnosticRecord(
                record.Sequence,
                record.TimestampUtc,
                record.Level.ToString(),
                record.Category.ToString(),
                record.EventName,
                record.Severity.ToString(),
                record.Direction?.ToString(),
                record.OperationId,
                record.EndpointId,
                record.AttachmentGeneration,
                instrumentId: null,
                descriptorPath: null,
                record.Duration,
                record.Outcome?.ToString(),
                record.Details,
                sessionContext: null,
                record.ByteSnapshot is null
                    ? null
                    : new ExportedDiagnosticByteSnapshot(
                        record.ByteSnapshot.OriginalByteCount,
                        record.ByteSnapshot.ToArray(),
                        record.ByteSnapshot.IsTruncated));
        }

        return new DiagnosticExportDocument(
            new DiagnosticExportEnvelope(
                DiagnosticExportApplications.RuntimeHost,
                captureLevel.ToString(),
                runtimeHostId,
                exportedAtUtc,
                exportedRecords.Length),
            exportedRecords);
    }
}

/// <summary>
/// Builds one export document from retained Client diagnostics.
/// The mapping carries exactly the captured, sanitized fields.
/// </summary>
public static class ClientDiagnosticExport
{
    public static DiagnosticExportDocument ToDocument(
        ClientDiagnosticLevel captureLevel,
        DateTimeOffset exportedAtUtc,
        IReadOnlyList<ClientDiagnosticRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var exportedRecords = new ExportedDiagnosticRecord[records.Count];
        for (int index = 0; index < records.Count; index++)
        {
            ClientDiagnosticRecord record = records[index]
                ?? throw new ArgumentException(
                    "A diagnostic record must not be null.",
                    nameof(records));

            exportedRecords[index] = new ExportedDiagnosticRecord(
                record.Sequence,
                record.TimestampUtc,
                record.Level.ToString(),
                record.Category.ToString(),
                record.EventName,
                record.Severity.ToString(),
                record.Direction?.ToString(),
                record.OperationId,
                record.EndpointId,
                record.AttachmentGeneration,
                record.InstrumentId,
                record.DescriptorPath,
                record.Duration,
                record.Outcome?.ToString(),
                record.Metadata,
                record.SessionContext is null
                    ? null
                    : new ExportedDiagnosticSessionContext(
                        record.SessionContext.ProfileId.Value,
                        record.SessionContext.ProfileDisplayName,
                        record.SessionContext.ExpectedRuntimeHostId.Value,
                        record.SessionContext.AuthoritativeRuntimeHostId?.Value),
                record.ByteSnapshot is null
                    ? null
                    : new ExportedDiagnosticByteSnapshot(
                        record.ByteSnapshot.OriginalByteCount,
                        record.ByteSnapshot.ToArray(),
                        record.ByteSnapshot.IsTruncated));
        }

        return new DiagnosticExportDocument(
            new DiagnosticExportEnvelope(
                DiagnosticExportApplications.Client,
                captureLevel.ToString(),
                runtimeHostId: null,
                exportedAtUtc,
                exportedRecords.Length),
            exportedRecords);
    }
}
