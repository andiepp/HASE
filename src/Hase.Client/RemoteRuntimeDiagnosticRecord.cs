using System.Collections.ObjectModel;

namespace Hase.Client;

/// <summary>Represents one immutable normalized remote diagnostic record.</summary>
public sealed record RemoteRuntimeDiagnosticRecord
{
    public RemoteRuntimeDiagnosticRecord(
        string runtimeHostId,
        long sourceSequence,
        DateTimeOffset timestampUtc,
        RemoteRuntimeDiagnosticLevel level,
        RemoteRuntimeDiagnosticCategory category,
        string eventName,
        RemoteRuntimeDiagnosticSeverity severity,
        string? endpointId = null,
        Guid? attachmentGeneration = null,
        RemoteRuntimeDiagnosticDirection? direction = null,
        Guid? operationId = null,
        TimeSpan? duration = null,
        RemoteRuntimeDiagnosticOutcome? outcome = null,
        IReadOnlyDictionary<string, string>? details = null,
        RemoteRuntimeDiagnosticByteSnapshot? byteSnapshot = null)
    {
        if (string.IsNullOrWhiteSpace(runtimeHostId))
        {
            throw new ArgumentException(
                "Runtime Host identity must not be empty.",
                nameof(runtimeHostId));
        }
        if (sourceSequence <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceSequence));
        }
        if (timestampUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Diagnostic timestamp must be UTC.",
                nameof(timestampUtc));
        }
        if (!Enum.IsDefined(level))
        {
            throw new ArgumentOutOfRangeException(nameof(level));
        }
        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(nameof(category));
        }
        if (string.IsNullOrWhiteSpace(eventName))
        {
            throw new ArgumentException(
                "Event name must not be empty.",
                nameof(eventName));
        }
        if (!Enum.IsDefined(severity))
        {
            throw new ArgumentOutOfRangeException(nameof(severity));
        }
        if (endpointId is not null && string.IsNullOrWhiteSpace(endpointId))
        {
            throw new ArgumentException(
                "Endpoint identity must not be empty when supplied.",
                nameof(endpointId));
        }
        if (direction.HasValue && !Enum.IsDefined(direction.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(direction));
        }
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }
        if (outcome.HasValue && !Enum.IsDefined(outcome.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome));
        }
        if (byteSnapshot is not null && level != RemoteRuntimeDiagnosticLevel.Bytes)
        {
            throw new ArgumentException(
                "A byte snapshot requires the Bytes diagnostic level.",
                nameof(byteSnapshot));
        }

        RuntimeHostId = runtimeHostId.Trim();
        SourceSequence = sourceSequence;
        TimestampUtc = timestampUtc;
        Level = level;
        Category = category;
        EventName = eventName.Trim();
        Severity = severity;
        EndpointId = endpointId?.Trim();
        AttachmentGeneration = attachmentGeneration;
        Direction = direction;
        OperationId = operationId;
        Duration = duration;
        Outcome = outcome;
        Details = new ReadOnlyDictionary<string, string>(
            details?.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal)
            ?? new Dictionary<string, string>(StringComparer.Ordinal));
        ByteSnapshot = byteSnapshot;
    }

    public string RuntimeHostId { get; }
    public long SourceSequence { get; }
    public DateTimeOffset TimestampUtc { get; }
    public RemoteRuntimeDiagnosticLevel Level { get; }
    public RemoteRuntimeDiagnosticCategory Category { get; }
    public string EventName { get; }
    public RemoteRuntimeDiagnosticSeverity Severity { get; }
    public string? EndpointId { get; }
    public Guid? AttachmentGeneration { get; }
    public RemoteRuntimeDiagnosticDirection? Direction { get; }
    public Guid? OperationId { get; }
    public TimeSpan? Duration { get; }
    public RemoteRuntimeDiagnosticOutcome? Outcome { get; }
    public IReadOnlyDictionary<string, string> Details { get; }
    public RemoteRuntimeDiagnosticByteSnapshot? ByteSnapshot { get; }
}
