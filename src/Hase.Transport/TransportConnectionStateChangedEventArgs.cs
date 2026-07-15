namespace Hase.Transport;

/// <summary>
/// Provides information about a transport connection state transition.
/// </summary>
public sealed class TransportConnectionStateChangedEventArgs
    : EventArgs
{
    /// <summary>
    /// Initializes a transport connection state-change notification.
    /// </summary>
    public TransportConnectionStateChangedEventArgs(
        TransportConnectionState previousState,
        TransportConnectionState currentState)
    {
        if (previousState == currentState)
        {
            throw new ArgumentException(
                "The previous and current transport connection states "
                + "must be different.",
                nameof(currentState));
        }

        PreviousState =
            previousState;

        CurrentState =
            currentState;
    }

    /// <summary>
    /// Gets the connection state before the transition.
    /// </summary>
    public TransportConnectionState PreviousState
    {
        get;
    }

    /// <summary>
    /// Gets the connection state after the transition.
    /// </summary>
    public TransportConnectionState CurrentState
    {
        get;
    }
}