namespace Hase.Runtime.Northbound;

/// <summary>
/// Identifies one record position within one live diagnostic projection
/// subscription.
/// </summary>
public sealed record RuntimeHostDiagnosticProjectionSequence
{
    public RuntimeHostDiagnosticProjectionSequence(long value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "A diagnostic projection sequence must be positive.");
        }

        Value = value;
    }

    public long Value { get; }
}
