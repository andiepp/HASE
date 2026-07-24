using Hase.Runtime.Connections;

namespace Hase.Runtime.Northbound;

/// <summary>
/// Describes a normalized connection-status change for one published
/// attachment.
/// </summary>
public sealed record RuntimeHostConnectionStatusChangedObservationPayload
    : RuntimeHostObservationPayload
{
    /// <summary>
    /// Initializes a connection-status-changed observation payload.
    /// </summary>
    public RuntimeHostConnectionStatusChangedObservationPayload(
        EndpointConnectionStatus previousStatus,
        EndpointConnectionStatus currentStatus)
    {
        PreviousStatus =
            previousStatus
            ?? throw new ArgumentNullException(
                nameof(previousStatus));

        CurrentStatus =
            currentStatus
            ?? throw new ArgumentNullException(
                nameof(currentStatus));
    }

    /// <inheritdoc />
    public override RuntimeHostObservationKind Kind =>
        RuntimeHostObservationKind.ConnectionStatusChanged;

    /// <summary>
    /// Gets the previous normalized connection status.
    /// </summary>
    public EndpointConnectionStatus PreviousStatus
    {
        get;
    }

    /// <summary>
    /// Gets the current normalized connection status.
    /// </summary>
    public EndpointConnectionStatus CurrentStatus
    {
        get;
    }
}