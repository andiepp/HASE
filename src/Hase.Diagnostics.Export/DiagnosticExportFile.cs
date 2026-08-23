using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hase.Diagnostics.Export;

/// <summary>
/// Writes and strictly reads versioned HASE diagnostic export documents.
/// One export is UTF-8 JSON Lines: an envelope line followed by one line
/// per record. Reading fails closed on every malformed input.
/// </summary>
public static class DiagnosticExportFile
{
    public const string DocumentKind = "hase-diagnostic-export";
    public const int CurrentFormatVersion = 1;
    public const int MaximumDocumentByteCount = 16 * 1024 * 1024;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        MaxDepth = 8
    };

    private static readonly UTF8Encoding Utf8WithoutBom = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    /// <summary>
    /// Writes one export document atomically, refusing existing targets.
    /// </summary>
    public static async Task WriteNewAsync(
        string filePath,
        DiagnosticExportDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        RequireFullyQualified(filePath);

        string fullPath = Path.GetFullPath(filePath);
        if (File.Exists(fullPath))
        {
            throw new IOException(
                "The diagnostic export target already exists.");
        }
        string? directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory)
            || !Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException(
                "The diagnostic export directory does not exist.");
        }

        var builder = new StringBuilder();
        builder.Append(JsonSerializer.Serialize(
            new EnvelopeLine
            {
                DocumentKind = DocumentKind,
                FormatVersion = CurrentFormatVersion,
                Application = document.Envelope.Application,
                CaptureLevel = document.Envelope.CaptureLevel,
                RuntimeHostId = document.Envelope.RuntimeHostId,
                ExportedAtUtc = document.Envelope.ExportedAtUtc,
                RecordCount = document.Envelope.RecordCount
            },
            SerializerOptions));
        builder.Append('\n');

        foreach (ExportedDiagnosticRecord record in document.Records)
        {
            builder.Append(JsonSerializer.Serialize(
                ToLine(record),
                SerializerOptions));
            builder.Append('\n');
        }

        string content = builder.ToString();
        if (Utf8WithoutBom.GetByteCount(content) > MaximumDocumentByteCount)
        {
            throw new InvalidDataException(
                "The diagnostic export exceeds the supported document size.");
        }

        string temporaryPath =
            fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(
                    temporaryPath,
                    content,
                    Utf8WithoutBom,
                    cancellationToken)
                .ConfigureAwait(false);
            File.Move(temporaryPath, fullPath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    /// <summary>
    /// Reads and strictly validates one export document.
    /// </summary>
    public static async Task<DiagnosticExportDocument> ReadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        RequireFullyQualified(filePath);

        byte[] document;
        await using (FileStream stream = new(
            Path.GetFullPath(filePath),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            if (stream.Length > MaximumDocumentByteCount)
            {
                throw new InvalidDataException(
                    "The diagnostic export exceeds the supported document size.");
            }

            byte[] buffer = new byte[(int)stream.Length];
            int length = 0;
            while (length < buffer.Length)
            {
                int read = await stream.ReadAsync(
                        buffer.AsMemory(length),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }
                length += read;
            }

            document = length == buffer.Length ? buffer : buffer[..length];
        }

        return Parse(document);
    }

    private static DiagnosticExportDocument Parse(byte[] document)
    {
        string text;
        try
        {
            ReadOnlySpan<byte> span = document;
            if (span.Length >= 3
                && span[0] == 0xEF && span[1] == 0xBB && span[2] == 0xBF)
            {
                span = span[3..];
            }
            text = Utf8WithoutBom.GetString(span);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException(
                "The diagnostic export is not valid UTF-8.",
                exception);
        }

        string[] lines = text
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.Length > 0)
            .ToArray();
        if (lines.Length == 0)
        {
            throw new InvalidDataException(
                "The diagnostic export contains no envelope.");
        }

        try
        {
            EnvelopeLine envelope =
                JsonSerializer.Deserialize<EnvelopeLine>(
                    lines[0],
                    SerializerOptions)
                ?? throw new InvalidDataException(
                    "The diagnostic export envelope must be a JSON object.");

            if (envelope.DocumentKind != DocumentKind)
            {
                throw new InvalidDataException(
                    "The document is not a HASE diagnostic export.");
            }
            if (envelope.FormatVersion != CurrentFormatVersion)
            {
                throw new InvalidDataException(
                    "The diagnostic export format version is not supported.");
            }
            if (envelope.Application is null
                || envelope.CaptureLevel is null
                || envelope.RecordCount is null)
            {
                throw new InvalidDataException(
                    "The diagnostic export envelope is incomplete.");
            }
            if (envelope.RecordCount.Value != lines.Length - 1)
            {
                throw new InvalidDataException(
                    "The diagnostic export record count does not match its records.");
            }

            var records = new ExportedDiagnosticRecord[lines.Length - 1];
            for (int index = 1; index < lines.Length; index++)
            {
                RecordLine line =
                    JsonSerializer.Deserialize<RecordLine>(
                        lines[index],
                        SerializerOptions)
                    ?? throw new InvalidDataException(
                        "A diagnostic export record must be a JSON object.");
                records[index - 1] = FromLine(line);
            }

            return new DiagnosticExportDocument(
                new DiagnosticExportEnvelope(
                    envelope.Application,
                    envelope.CaptureLevel,
                    envelope.RuntimeHostId,
                    envelope.ExportedAtUtc
                        ?? throw new InvalidDataException(
                            "The diagnostic export timestamp is required."),
                    envelope.RecordCount.Value),
                records);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The diagnostic export is not valid JSON Lines content.",
                exception);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException(
                "The diagnostic export contains invalid content.",
                exception);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException(
                "The diagnostic export contains invalid content.",
                exception);
        }
    }

    private static RecordLine ToLine(ExportedDiagnosticRecord record) =>
        new()
        {
            Sequence = record.Sequence,
            TimestampUtc = record.TimestampUtc,
            Level = record.Level,
            Category = record.Category,
            EventName = record.EventName,
            Severity = record.Severity,
            Direction = record.Direction,
            OperationId = record.OperationId,
            EndpointId = record.EndpointId,
            AttachmentGeneration = record.AttachmentGeneration,
            InstrumentId = record.InstrumentId,
            DescriptorPath = record.DescriptorPath,
            Duration = record.Duration,
            Outcome = record.Outcome,
            Details = record.Details.Count == 0
                ? null
                : record.Details.ToDictionary(
                    item => item.Key,
                    item => item.Value,
                    StringComparer.Ordinal),
            SessionContext = record.SessionContext is null
                ? null
                : new SessionContextLine
                {
                    ProfileId = record.SessionContext.ProfileId,
                    ProfileDisplayName =
                        record.SessionContext.ProfileDisplayName,
                    ExpectedRuntimeHostId =
                        record.SessionContext.ExpectedRuntimeHostId,
                    AuthoritativeRuntimeHostId =
                        record.SessionContext.AuthoritativeRuntimeHostId
                },
            ByteSnapshot = record.ByteSnapshot is null
                ? null
                : new ByteSnapshotLine
                {
                    OriginalByteCount =
                        record.ByteSnapshot.OriginalByteCount,
                    CapturedHex = Convert.ToHexString(
                        record.ByteSnapshot.CapturedBytes.ToArray()),
                    IsTruncated = record.ByteSnapshot.IsTruncated
                }
        };

    private static ExportedDiagnosticRecord FromLine(RecordLine line) =>
        new(
            line.Sequence
                ?? throw new InvalidDataException(
                    "A diagnostic export record requires a sequence."),
            line.TimestampUtc
                ?? throw new InvalidDataException(
                    "A diagnostic export record requires a timestamp."),
            line.Level
                ?? throw new InvalidDataException(
                    "A diagnostic export record requires a level."),
            line.Category
                ?? throw new InvalidDataException(
                    "A diagnostic export record requires a category."),
            line.EventName
                ?? throw new InvalidDataException(
                    "A diagnostic export record requires an event name."),
            line.Severity
                ?? throw new InvalidDataException(
                    "A diagnostic export record requires a severity."),
            line.Direction,
            line.OperationId,
            line.EndpointId,
            line.AttachmentGeneration,
            line.InstrumentId,
            line.DescriptorPath,
            line.Duration,
            line.Outcome,
            line.Details is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(
                    line.Details,
                    StringComparer.Ordinal),
            line.SessionContext is null
                ? null
                : new ExportedDiagnosticSessionContext(
                    line.SessionContext.ProfileId
                        ?? throw new InvalidDataException(
                            "A session context requires a profile identity."),
                    line.SessionContext.ProfileDisplayName
                        ?? throw new InvalidDataException(
                            "A session context requires a display name."),
                    line.SessionContext.ExpectedRuntimeHostId
                        ?? throw new InvalidDataException(
                            "A session context requires an expected identity."),
                    line.SessionContext.AuthoritativeRuntimeHostId),
            line.ByteSnapshot is null
                ? null
                : new ExportedDiagnosticByteSnapshot(
                    line.ByteSnapshot.OriginalByteCount
                        ?? throw new InvalidDataException(
                            "A byte snapshot requires an original count."),
                    Convert.FromHexString(
                        line.ByteSnapshot.CapturedHex
                        ?? throw new InvalidDataException(
                            "A byte snapshot requires captured content.")),
                    line.ByteSnapshot.IsTruncated
                        ?? throw new InvalidDataException(
                            "A byte snapshot requires a truncation status.")));

    private static void RequireFullyQualified(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!Path.IsPathFullyQualified(filePath))
        {
            throw new ArgumentException(
                "The diagnostic export path must be fully qualified.",
                nameof(filePath));
        }
    }

    private sealed class EnvelopeLine
    {
        [JsonPropertyName("documentKind")]
        public string? DocumentKind { get; set; }
        [JsonPropertyName("formatVersion")]
        public int FormatVersion { get; set; }
        [JsonPropertyName("application")]
        public string? Application { get; set; }
        [JsonPropertyName("captureLevel")]
        public string? CaptureLevel { get; set; }
        [JsonPropertyName("runtimeHostId")]
        public string? RuntimeHostId { get; set; }
        [JsonPropertyName("exportedAtUtc")]
        public DateTimeOffset? ExportedAtUtc { get; set; }
        [JsonPropertyName("recordCount")]
        public int? RecordCount { get; set; }
    }

    private sealed class RecordLine
    {
        [JsonPropertyName("sequence")]
        public long? Sequence { get; set; }
        [JsonPropertyName("timestampUtc")]
        public DateTimeOffset? TimestampUtc { get; set; }
        [JsonPropertyName("level")]
        public string? Level { get; set; }
        [JsonPropertyName("category")]
        public string? Category { get; set; }
        [JsonPropertyName("eventName")]
        public string? EventName { get; set; }
        [JsonPropertyName("severity")]
        public string? Severity { get; set; }
        [JsonPropertyName("direction")]
        public string? Direction { get; set; }
        [JsonPropertyName("operationId")]
        public Guid? OperationId { get; set; }
        [JsonPropertyName("endpointId")]
        public string? EndpointId { get; set; }
        [JsonPropertyName("attachmentGeneration")]
        public Guid? AttachmentGeneration { get; set; }
        [JsonPropertyName("instrumentId")]
        public string? InstrumentId { get; set; }
        [JsonPropertyName("descriptorPath")]
        public string? DescriptorPath { get; set; }
        [JsonPropertyName("duration")]
        public TimeSpan? Duration { get; set; }
        [JsonPropertyName("outcome")]
        public string? Outcome { get; set; }
        [JsonPropertyName("details")]
        public Dictionary<string, string>? Details { get; set; }
        [JsonPropertyName("sessionContext")]
        public SessionContextLine? SessionContext { get; set; }
        [JsonPropertyName("byteSnapshot")]
        public ByteSnapshotLine? ByteSnapshot { get; set; }
    }

    private sealed class SessionContextLine
    {
        [JsonPropertyName("profileId")]
        public string? ProfileId { get; set; }
        [JsonPropertyName("profileDisplayName")]
        public string? ProfileDisplayName { get; set; }
        [JsonPropertyName("expectedRuntimeHostId")]
        public string? ExpectedRuntimeHostId { get; set; }
        [JsonPropertyName("authoritativeRuntimeHostId")]
        public string? AuthoritativeRuntimeHostId { get; set; }
    }

    private sealed class ByteSnapshotLine
    {
        [JsonPropertyName("originalByteCount")]
        public int? OriginalByteCount { get; set; }
        [JsonPropertyName("capturedHex")]
        public string? CapturedHex { get; set; }
        [JsonPropertyName("isTruncated")]
        public bool? IsTruncated { get; set; }
    }
}
