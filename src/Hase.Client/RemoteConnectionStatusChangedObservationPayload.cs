namespace Hase.Client;

/// <summary>
/// Describes one physical connection-status change for a published remote
/// endpoint attachment.
/// </summary>
public sealed record RemoteConnectionStatusChangedObservationPayload
    : RemoteObservationPayload
{
    /// <summary>
    /// Initializes one connection-status-changed observation payload.
    /// </summary>
    public RemoteConnectionStatusChangedObservationPayload(
        RemoteEndpointConnectionStatus previousStatus,
        RemoteEndpointConnectionStatus currentStatus)
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
    public override RemoteObservationKind Kind =>
        RemoteObservationKind.ConnectionStatusChanged;

    /// <summary>
    /// Gets the previous physical endpoint connection status.
    /// </summary>
    public RemoteEndpointConnectionStatus PreviousStatus
    {
        get;
    }

    /// <summary>
    /// Gets the current physical endpoint connection status.
    /// </summary>
    public RemoteEndpointConnectionStatus CurrentStatus
    {
        get;
    }
}
