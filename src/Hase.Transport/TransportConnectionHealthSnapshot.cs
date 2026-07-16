namespace Hase.Transport;

/// <summary>
/// Represents an immutable diagnostic snapshot of transport connection health.
/// </summary>
public sealed record TransportConnectionHealthSnapshot
{
    /// <summary>
    /// Initializes a transport connection health snapshot.
    /// </summary>
    /// <param name="hasConnection">
    /// Indicates whether a current transport connection exists.
    /// </param>
    /// <param name="state">
    /// Current connection state, or <see langword="null"/> when no
    /// connection exists.
    /// </param>
    /// <param name="lastStateChangeUtc">
    /// UTC time at which the current connection was established or its
    /// most recent state transition was observed.
    /// </param>
    /// <param name="replacementCount">
    /// Number of successfully completed faulted-connection replacements.
    /// </param>
    public TransportConnectionHealthSnapshot(
        bool hasConnection,
        TransportConnectionState? state,
        DateTimeOffset? lastStateChangeUtc,
        int replacementCount)
    {
        if (hasConnection
            && state is null)
        {
            throw new ArgumentException(
                "A health snapshot with a current connection must "
                + "contain a connection state.",
                nameof(state));
        }

        if (!hasConnection
            && state is not null)
        {
            throw new ArgumentException(
                "A health snapshot without a current connection must "
                + "not contain a connection state.",
                nameof(state));
        }

        if (replacementCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(replacementCount),
                replacementCount,
                "The replacement count must not be negative.");
        }

        HasConnection =
            hasConnection;

        State =
            state;

        LastStateChangeUtc =
            lastStateChangeUtc;

        ReplacementCount =
            replacementCount;
    }

    /// <summary>
    /// Gets a value indicating whether a current connection exists.
    /// </summary>
    public bool HasConnection
    {
        get;
    }

    /// <summary>
    /// Gets the current connection state,
    /// or <see langword="null"/> when no connection exists.
    /// </summary>
    public TransportConnectionState? State
    {
        get;
    }

    /// <summary>
    /// Gets the UTC time of the current connection establishment or its
    /// most recently observed state transition.
    /// </summary>
    public DateTimeOffset? LastStateChangeUtc
    {
        get;
    }

    /// <summary>
    /// Gets the number of successfully completed faulted-connection
    /// replacements.
    /// </summary>
    public int ReplacementCount
    {
        get;
    }
}