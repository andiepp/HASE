namespace Hase.Client.Diagnostics;

/// <summary>
/// Represents one immutable, UTC-stamped, process-local client diagnostic.
/// </summary>
public sealed record ClientDiagnosticRecord
{
    internal ClientDiagnosticRecord(
        long sequence,
        DateTimeOffset timestampUtc,
        ClientDiagnosticEvent diagnosticEvent)
    {
        if (sequence <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequence), sequence, "Sequence must be positive.");
        }

        ArgumentNullException.ThrowIfNull(diagnosticEvent);

        Sequence = sequence;
        TimestampUtc = timestampUtc.ToUniversalTime();
        Level = diagnosticEvent.Level;
        Category = diagnosticEvent.Category;
        EventName = diagnosticEvent.EventName;
        Severity = diagnosticEvent.Severity;
        Direction = diagnosticEvent.Direction;
        OperationId = diagnosticEvent.OperationId;
        EndpointId = diagnosticEvent.EndpointId;
        AttachmentGeneration = diagnosticEvent.AttachmentGeneration;
        InstrumentId = diagnosticEvent.InstrumentId;
        DescriptorPath = diagnosticEvent.DescriptorPath;
        Duration = diagnosticEvent.Duration;
        Outcome = diagnosticEvent.Outcome;
        Metadata = diagnosticEvent.Metadata;
    }

    public long Sequence { get; }
    public DateTimeOffset TimestampUtc { get; }
    public ClientDiagnosticLevel Level { get; }
    public ClientDiagnosticCategory Category { get; }
    public string EventName { get; }
    public ClientDiagnosticSeverity Severity { get; }
    public ClientDiagnosticDirection? Direction { get; }
    public Guid? OperationId { get; }
    public string? EndpointId { get; }
    public Guid? AttachmentGeneration { get; }
    public string? InstrumentId { get; }
    public string? DescriptorPath { get; }
    public TimeSpan? Duration { get; }
    public ClientDiagnosticOutcome? Outcome { get; }
    public IReadOnlyDictionary<string, string> Metadata { get; }
}
