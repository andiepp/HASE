namespace Hase.Client;

/// <summary>
/// Identifies one opaque position within one remote observation subscription.
/// </summary>
/// <remarks>
/// A sequence is meaningful only within the subscription that issued it. It
/// is not a persistent host-wide Event position and must not be compared with
/// a sequence from another subscription.
/// </remarks>
public sealed record RemoteObservationSequence
{
    /// <summary>
    /// Initializes one subscription-local observation sequence.
    /// </summary>
    public RemoteObservationSequence(
        ulong value)
    {
        Value =
            value;
    }

    /// <summary>
    /// Gets the opaque subscription-local sequence value.
    /// </summary>
    public ulong Value
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
