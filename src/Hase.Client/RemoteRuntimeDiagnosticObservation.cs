namespace Hase.Client;

/// <summary>Carries one remote diagnostic with its subscription sequence.</summary>
public sealed record RemoteRuntimeDiagnosticObservation
{
    public RemoteRuntimeDiagnosticObservation(
        long sequence,
        RemoteRuntimeDiagnosticRecord record)
    {
        if (sequence <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence));
        }

        Sequence = sequence;
        Record = record ?? throw new ArgumentNullException(nameof(record));
    }

    public long Sequence { get; }

    public RemoteRuntimeDiagnosticRecord Record { get; }
}
