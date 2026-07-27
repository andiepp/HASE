namespace Hase.Client;

/// <summary>
/// Represents one immutable physical endpoint connection status observed
/// through a remote runtime host.
/// </summary>
public sealed record RemoteEndpointConnectionStatus
{
    /// <summary>
    /// Initializes one remote endpoint connection status.
    /// </summary>
    public RemoteEndpointConnectionStatus(
        RemoteEndpointConnectionState state,
        DateTimeOffset? changedAtUtc = null,
        string? detail = null)
    {
        if (!Enum.IsDefined(
                state)
            || state == RemoteEndpointConnectionState.Unspecified)
        {
            throw new ArgumentOutOfRangeException(
                nameof(state),
                state,
                "A specified remote endpoint connection state is required.");
        }

        if (changedAtUtc.HasValue
            && changedAtUtc.Value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The remote endpoint status timestamp must be expressed in "
                + "UTC.",
                nameof(changedAtUtc));
        }

        State =
            state;
        ChangedAtUtc =
            changedAtUtc;
        Detail =
            string.IsNullOrWhiteSpace(
                detail)
                ? null
                : detail.Trim();
    }

    /// <summary>
    /// Gets the normalized physical endpoint connection state.
    /// </summary>
    public RemoteEndpointConnectionState State
    {
        get;
    }

    /// <summary>
    /// Gets the UTC time at which the status became active, when supplied by
    /// the runtime host.
    /// </summary>
    public DateTimeOffset? ChangedAtUtc
    {
        get;
    }

    /// <summary>
    /// Gets optional diagnostic information.
    /// </summary>
    /// <remarks>
    /// Diagnostic information must not be used for client program logic.
    /// </remarks>
    public string? Detail
    {
        get;
    }
}
