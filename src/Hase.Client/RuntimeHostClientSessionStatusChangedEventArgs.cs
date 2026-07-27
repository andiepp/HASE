namespace Hase.Client;

/// <summary>
/// Describes one normalized runtime-host client-session status transition.
/// </summary>
public sealed class RuntimeHostClientSessionStatusChangedEventArgs
    : EventArgs
{
    public RuntimeHostClientSessionStatusChangedEventArgs(
        RuntimeHostClientSessionStatus previous,
        RuntimeHostClientSessionStatus current)
    {
        Previous =
            previous
            ?? throw new ArgumentNullException(
                nameof(previous));
        Current =
            current
            ?? throw new ArgumentNullException(
                nameof(current));

        if (Previous == Current)
        {
            throw new ArgumentException(
                "A session-status transition requires different statuses.",
                nameof(current));
        }
    }

    /// <summary>
    /// Gets the status before the transition.
    /// </summary>
    public RuntimeHostClientSessionStatus Previous
    {
        get;
    }

    /// <summary>
    /// Gets the status after the transition.
    /// </summary>
    public RuntimeHostClientSessionStatus Current
    {
        get;
    }
}
