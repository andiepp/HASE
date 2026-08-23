namespace Hase.Diagnostics.Export;

/// <summary>
/// Identifies the application that produced a diagnostic export.
/// </summary>
public static class DiagnosticExportApplications
{
    public const string RuntimeHost = "runtime-host";
    public const string Client = "client";

    public static bool IsKnown(string value) =>
        value is RuntimeHost or Client;
}

/// <summary>
/// The envelope of one diagnostic export document.
/// </summary>
public sealed record DiagnosticExportEnvelope
{
    public DiagnosticExportEnvelope(
        string application,
        string captureLevel,
        string? runtimeHostId,
        DateTimeOffset exportedAtUtc,
        int recordCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(application);
        if (!DiagnosticExportApplications.IsKnown(application))
        {
            throw new ArgumentException(
                "The exporting application is not recognized.",
                nameof(application));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(captureLevel);
        if (runtimeHostId is not null
            && string.IsNullOrWhiteSpace(runtimeHostId))
        {
            throw new ArgumentException(
                "A runtime-host identity must not be empty or whitespace.",
                nameof(runtimeHostId));
        }
        ArgumentOutOfRangeException.ThrowIfNegative(recordCount);

        Application = application;
        CaptureLevel = captureLevel;
        RuntimeHostId = runtimeHostId;
        ExportedAtUtc = exportedAtUtc.ToUniversalTime();
        RecordCount = recordCount;
    }

    public string Application { get; }
    public string CaptureLevel { get; }
    public string? RuntimeHostId { get; }
    public DateTimeOffset ExportedAtUtc { get; }
    public int RecordCount { get; }
}

/// <summary>
/// One bounded exported byte snapshot, preserving the capture invariants.
/// </summary>
public sealed record ExportedDiagnosticByteSnapshot
{
    public const int MaximumCapturedByteCount = 256;

    public ExportedDiagnosticByteSnapshot(
        int originalByteCount,
        byte[] capturedBytes,
        bool isTruncated)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(originalByteCount);
        ArgumentNullException.ThrowIfNull(capturedBytes);
        if (capturedBytes.Length > MaximumCapturedByteCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capturedBytes),
                capturedBytes.Length,
                "Captured bytes must not exceed the capture bound.");
        }
        if (capturedBytes.Length > originalByteCount)
        {
            throw new ArgumentException(
                "Captured byte count must not exceed the original byte count.",
                nameof(capturedBytes));
        }
        if (isTruncated != capturedBytes.Length < originalByteCount)
        {
            throw new ArgumentException(
                "Truncation status must match the original and captured byte counts.",
                nameof(isTruncated));
        }

        OriginalByteCount = originalByteCount;
        CapturedBytes = capturedBytes.ToArray();
        IsTruncated = isTruncated;
    }

    public int OriginalByteCount { get; }
    public IReadOnlyList<byte> CapturedBytes { get; }
    public bool IsTruncated { get; }
}

/// <summary>
/// The exported client session context of one record.
/// </summary>
public sealed record ExportedDiagnosticSessionContext
{
    public ExportedDiagnosticSessionContext(
        string profileId,
        string profileDisplayName,
        string expectedRuntimeHostId,
        string? authoritativeRuntimeHostId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileDisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedRuntimeHostId);
        if (authoritativeRuntimeHostId is not null
            && string.IsNullOrWhiteSpace(authoritativeRuntimeHostId))
        {
            throw new ArgumentException(
                "An authoritative runtime-host identity must not be empty.",
                nameof(authoritativeRuntimeHostId));
        }

        ProfileId = profileId;
        ProfileDisplayName = profileDisplayName;
        ExpectedRuntimeHostId = expectedRuntimeHostId;
        AuthoritativeRuntimeHostId = authoritativeRuntimeHostId;
    }

    public string ProfileId { get; }
    public string ProfileDisplayName { get; }
    public string ExpectedRuntimeHostId { get; }
    public string? AuthoritativeRuntimeHostId { get; }
}

/// <summary>
/// One exported diagnostic record, neutral over the producing application.
/// Enumerated values are carried as their exact source names.
/// </summary>
public sealed record ExportedDiagnosticRecord
{
    public ExportedDiagnosticRecord(
        long sequence,
        DateTimeOffset timestampUtc,
        string level,
        string category,
        string eventName,
        string severity,
        string? direction,
        Guid? operationId,
        string? endpointId,
        Guid? attachmentGeneration,
        string? instrumentId,
        string? descriptorPath,
        TimeSpan? duration,
        string? outcome,
        IReadOnlyDictionary<string, string> details,
        ExportedDiagnosticSessionContext? sessionContext,
        ExportedDiagnosticByteSnapshot? byteSnapshot)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence);
        ArgumentException.ThrowIfNullOrWhiteSpace(level);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentException.ThrowIfNullOrWhiteSpace(severity);
        ArgumentNullException.ThrowIfNull(details);

        Sequence = sequence;
        TimestampUtc = timestampUtc.ToUniversalTime();
        Level = level;
        Category = category;
        EventName = eventName;
        Severity = severity;
        Direction = direction;
        OperationId = operationId;
        EndpointId = endpointId;
        AttachmentGeneration = attachmentGeneration;
        InstrumentId = instrumentId;
        DescriptorPath = descriptorPath;
        Duration = duration;
        Outcome = outcome;
        Details = details;
        SessionContext = sessionContext;
        ByteSnapshot = byteSnapshot;
    }

    public long Sequence { get; }
    public DateTimeOffset TimestampUtc { get; }
    public string Level { get; }
    public string Category { get; }
    public string EventName { get; }
    public string Severity { get; }
    public string? Direction { get; }
    public Guid? OperationId { get; }
    public string? EndpointId { get; }
    public Guid? AttachmentGeneration { get; }
    public string? InstrumentId { get; }
    public string? DescriptorPath { get; }
    public TimeSpan? Duration { get; }
    public string? Outcome { get; }
    public IReadOnlyDictionary<string, string> Details { get; }
    public ExportedDiagnosticSessionContext? SessionContext { get; }
    public ExportedDiagnosticByteSnapshot? ByteSnapshot { get; }
}

/// <summary>
/// One complete diagnostic export: the envelope and its records.
/// </summary>
public sealed record DiagnosticExportDocument
{
    public DiagnosticExportDocument(
        DiagnosticExportEnvelope envelope,
        IReadOnlyList<ExportedDiagnosticRecord> records)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(records);
        if (envelope.RecordCount != records.Count)
        {
            throw new ArgumentException(
                "The envelope record count must match the record list.",
                nameof(records));
        }

        Envelope = envelope;
        Records = records.ToArray();
    }

    public DiagnosticExportEnvelope Envelope { get; }
    public IReadOnlyList<ExportedDiagnosticRecord> Records { get; }
}
