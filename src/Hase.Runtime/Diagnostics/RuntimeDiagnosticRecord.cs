namespace Hase.Runtime.Diagnostics;

/// <summary>
/// Represents one immutable, UTC-stamped, process-local diagnostic record.
/// </summary>
public sealed record RuntimeDiagnosticRecord
{
    internal RuntimeDiagnosticRecord(
        long sequence,
        DateTimeOffset timestampUtc,
        RuntimeDiagnosticEvent diagnosticEvent)
    {
        if (sequence <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequence),
                sequence,
                "Sequence must be positive.");
        }

        ArgumentNullException.ThrowIfNull(
            diagnosticEvent);

        Sequence = sequence;
        TimestampUtc = timestampUtc.ToUniversalTime();
        Level = diagnosticEvent.Level;
        Category = diagnosticEvent.Category;
        EventName = diagnosticEvent.EventName;
        Severity = diagnosticEvent.Severity;
        EndpointId = diagnosticEvent.EndpointId;
        AttachmentGeneration =
            diagnosticEvent.AttachmentGeneration;
        Direction = diagnosticEvent.Direction;
        OperationId = diagnosticEvent.OperationId;
        Duration = diagnosticEvent.Duration;
        Outcome = diagnosticEvent.Outcome;
        Details = diagnosticEvent.Details;
        ByteSnapshot =
            diagnosticEvent.ByteSnapshot;
    }

    public long Sequence { get; }

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

    public RuntimeDiagnosticByteSnapshot? ByteSnapshot { get; }
}
