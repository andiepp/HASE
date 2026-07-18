using Hase.Protocol;
using Hase.Runtime.Runtime;

namespace Hase.Runtime.Transport;

/// <summary>
/// Routes decoded protocol event notifications into an existing runtime
/// endpoint graph.
/// </summary>
public sealed class ProtocolRuntimeEndpointEventRouter
    : IProtocolNotificationObserver
{
    private readonly RuntimeEndpoint _runtimeEndpoint;

    /// <summary>
    /// Initializes an event-notification router for one runtime endpoint.
    /// </summary>
    public ProtocolRuntimeEndpointEventRouter(
        RuntimeEndpoint runtimeEndpoint)
    {
        _runtimeEndpoint =
            runtimeEndpoint
            ?? throw new ArgumentNullException(
                nameof(runtimeEndpoint));
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

        RuntimeInstrument? runtimeInstrument =
            _runtimeEndpoint.FindInstrument(
                eventNotification.InstrumentId);

        if (runtimeInstrument is null)
        {
            return;
        }

        RuntimeEvent? runtimeEvent =
            runtimeInstrument.FindEvent(
                eventNotification.EventPath);

        if (runtimeEvent is null)
        {
            return;
        }

        runtimeEvent.PublishOccurrence(
            eventNotification.TimestampUtc,
            eventNotification.Value);
    }
}