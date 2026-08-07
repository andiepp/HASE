using System.Collections.ObjectModel;
using Hase.Runtime.Diagnostics;

namespace Hase.Runtime.Northbound;

/// <summary>
/// Represents one immutable Runtime Host diagnostic record approved for remote
/// projection. Source sequence is process-local and is not a subscription
/// sequence.
/// </summary>
public sealed record RuntimeHostProjectedDiagnosticRecord
{
    internal RuntimeHostProjectedDiagnosticRecord(
        RuntimeHostId runtimeHostId,
        long sourceSequence,
        DateTimeOffset timestampUtc,
        RuntimeDiagnosticLevel level,
        RuntimeDiagnosticCategory category,
        string eventName,
        RuntimeDiagnosticSeverity severity,
        string? endpointId,
        Guid? attachmentGeneration,
        RuntimeDiagnosticDirection? direction,
        Guid? operationId,
        TimeSpan? duration,
        RuntimeDiagnosticOutcome? outcome,
        IReadOnlyDictionary<string, string>? details,
        RuntimeHostProjectedDiagnosticByteSnapshot? byteSnapshot)
    {
        RuntimeHostId = runtimeHostId
            ?? throw new ArgumentNullException(nameof(runtimeHostId));

        if (sourceSequence <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceSequence),
                sourceSequence,
                "Source sequence must be positive.");
        }

        if (!Enum.IsDefined(level))
        {
            throw new ArgumentOutOfRangeException(nameof(level));
        }

        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(nameof(category));
        }

        if (!Enum.IsDefined(severity))
        {
            throw new ArgumentOutOfRangeException(nameof(severity));
        }

        if (string.IsNullOrWhiteSpace(eventName))
        {
            throw new ArgumentException(
                "Event name must not be empty.",
                nameof(eventName));
        }

        if (endpointId is not null && string.IsNullOrWhiteSpace(endpointId))
        {
            throw new ArgumentException(
                "Endpoint identity must not be empty when supplied.",
                nameof(endpointId));
        }

        if (direction is not null && !Enum.IsDefined(direction.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(direction));
        }

        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        if (outcome is not null && !Enum.IsDefined(outcome.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome));
        }

        if (byteSnapshot is not null && level != RuntimeDiagnosticLevel.Bytes)
        {
            throw new ArgumentException(
                "A projected byte snapshot requires the Bytes level.",
                nameof(byteSnapshot));
        }

        SourceSequence = sourceSequence;
        TimestampUtc = timestampUtc.ToUniversalTime();
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

    public RuntimeHostId RuntimeHostId { get; }

    public long SourceSequence { get; }

    public DateTimeOffset TimestampUtc { get; }

    public RuntimeDiagnosticLevel Level { get; }

    public RuntimeDiagnosticCategory Category { get; }

    public string EventName { get; }

    public RuntimeDiagnosticSeverity Severity { get; }

    public string? EndpointId { get; }

    public Guid? AttachmentGeneration { get; }

    public RuntimeDiagnosticDirection? Direction { get; }

    public Guid? OperationId { get; }

    public TimeSpan? Duration { get; }

    public RuntimeDiagnosticOutcome? Outcome { get; }

    public IReadOnlyDictionary<string, string> Details { get; }

    public RuntimeHostProjectedDiagnosticByteSnapshot? ByteSnapshot { get; }
}
