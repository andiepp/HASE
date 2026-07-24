namespace Hase.Runtime.Northbound;

/// <summary>
/// Identifies one position within one runtime-host observation subscription.
/// </summary>
/// <remarks>
/// A sequence is meaningful only within the subscription that issued it. It is
/// not a persistent host-wide event-log position and must not be compared with
/// a sequence from another subscription.
/// </remarks>
public sealed record RuntimeHostObservationSequence
{
    /// <summary>
    /// Initializes an observation sequence.
    /// </summary>
    public RuntimeHostObservationSequence(
        long value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "An observation sequence value must not be negative.");
        }

        Value =
            value;
    }

    /// <summary>
    /// Gets the subscription-local opaque sequence value.
    /// </summary>
    public long Value
    {
        get;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
    }
}