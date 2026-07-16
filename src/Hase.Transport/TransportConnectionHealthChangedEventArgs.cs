namespace Hase.Transport;

/// <summary>
/// Provides immutable transport-health snapshots from before and after
/// a health change.
/// </summary>
public sealed class TransportConnectionHealthChangedEventArgs
    : EventArgs
{
    /// <summary>
    /// Initializes a transport-health change notification.
    /// </summary>
    /// <param name="previousHealth">
    /// Transport health before the change.
    /// </param>
    /// <param name="currentHealth">
    /// Transport health after the change.
    /// </param>
    public TransportConnectionHealthChangedEventArgs(
        TransportConnectionHealthSnapshot previousHealth,
        TransportConnectionHealthSnapshot currentHealth)
    {
        PreviousHealth =
            previousHealth
            ?? throw new ArgumentNullException(
                nameof(previousHealth));

        CurrentHealth =
            currentHealth
            ?? throw new ArgumentNullException(
                nameof(currentHealth));

        if (PreviousHealth == CurrentHealth)
        {
            throw new ArgumentException(
                "The previous and current transport-health snapshots "
                + "must be different.",
                nameof(currentHealth));
        }
    }

    /// <summary>
    /// Gets the transport health before the change.
    /// </summary>
    public TransportConnectionHealthSnapshot PreviousHealth
    {
        get;
    }

    /// <summary>
    /// Gets the transport health after the change.
    /// </summary>
    public TransportConnectionHealthSnapshot CurrentHealth
    {
        get;
    }
}