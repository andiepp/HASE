namespace Hase.Runtime.Connections;

/// <summary>
/// Represents the current connection status of a runtime endpoint.
/// </summary>
public sealed record EndpointConnectionStatus
{
    public EndpointConnectionStatus(
        EndpointConnectionState state,
        DateTimeOffset? changedAtUtc = null,
        string? detail = null)
    {
        if (changedAtUtc.HasValue &&
            changedAtUtc.Value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The status timestamp must be expressed in UTC.",
                nameof(changedAtUtc));
        }

        State = state;
        ChangedAtUtc = changedAtUtc;
        Detail = string.IsNullOrWhiteSpace(detail)
            ? null
            : detail;
    }

    public EndpointConnectionState State { get; }

    /// <summary>
    /// Gets the UTC time at which this status became active.
    /// A null value means that no lifecycle service has updated
    /// the initial status yet.
    /// </summary>
    public DateTimeOffset? ChangedAtUtc { get; }

    /// <summary>
    /// Gets optional diagnostic information.
    /// This must not be used for program logic.
    /// </summary>
    public string? Detail { get; }
}
