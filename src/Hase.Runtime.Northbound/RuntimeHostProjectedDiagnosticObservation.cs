namespace Hase.Runtime.Northbound;

/// <summary>
/// Carries one immutable record with its subscription-local delivery sequence.
/// </summary>
public sealed record RuntimeHostProjectedDiagnosticObservation
{
    public RuntimeHostProjectedDiagnosticObservation(
        RuntimeHostDiagnosticProjectionSequence sequence,
        RuntimeHostProjectedDiagnosticRecord record)
    {
        Sequence = sequence
            ?? throw new ArgumentNullException(nameof(sequence));
        Record = record
            ?? throw new ArgumentNullException(nameof(record));
    }

    public RuntimeHostDiagnosticProjectionSequence Sequence { get; }

    public RuntimeHostProjectedDiagnosticRecord Record { get; }
}
