using Hase.Protocol;
using Hase.Runtime.Runtime;

namespace Hase.Runtime.Transport;

/// <summary>
/// Routes decoded Protocol Version 1 event notifications into an existing
/// runtime endpoint graph.
/// </summary>
public sealed class ProtocolRuntimeEndpointEventRouter
    : IProtocolNotificationObserver
{
    private readonly RuntimeEndpointEventRouter _eventRouter;

    /// <summary>
    /// Initializes an event-notification router for one runtime endpoint.
    /// </summary>
    public ProtocolRuntimeEndpointEventRouter(
        RuntimeEndpoint runtimeEndpoint)
    {
        _eventRouter =
            new RuntimeEndpointEventRouter(
                runtimeEndpoint
                ?? throw new ArgumentNullException(
                    nameof(runtimeEndpoint)));
    }

    /// <inheritdoc />
    public void OnProtocolNotification(
        ProtocolMessage notification)
    {
        ArgumentNullException.ThrowIfNull(
            notification);

        if (notification
            is not EventNotification eventNotification)
        {
            return;
        }

        _eventRouter.Publish(
            eventNotification.InstrumentId,
            eventNotification.EventPath,
            eventNotification.TimestampUtc,
            eventNotification.Value);
    }
}