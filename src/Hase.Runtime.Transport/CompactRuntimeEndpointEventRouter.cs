using Hase.CompactProtocol;
using Hase.Runtime.Runtime;

namespace Hase.Runtime.Transport;

/// <summary>
/// Routes current-connection-authoritative compact event notifications into
/// the existing runtime event graph.
/// </summary>
internal sealed class CompactRuntimeEndpointEventRouter
{
    private readonly RuntimeEndpointEventRouter _eventRouter;
    private readonly TimeProvider _timeProvider;

    public CompactRuntimeEndpointEventRouter(
        CompactMappedEventNotificationSource eventSource,
        RuntimeEndpoint runtimeEndpoint,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(
            eventSource);

        _eventRouter =
            new RuntimeEndpointEventRouter(
                runtimeEndpoint
                ?? throw new ArgumentNullException(
                    nameof(runtimeEndpoint)));

        _timeProvider =
            timeProvider
            ?? throw new ArgumentNullException(
                nameof(timeProvider));

        eventSource.MappedEventNotificationReceived +=
            OnMappedEventNotificationReceived;
    }

    private void OnMappedEventNotificationReceived(
        CompactMappedEventNotification mappedNotification)
    {
        ArgumentNullException.ThrowIfNull(
            mappedNotification);

        object? value =
            mappedNotification.Mapping.Encoding switch
            {
                CompactEventValueEncoding.None =>
                    null,

                _ =>
                    throw new InvalidDataException(
                        $"Compact event identifier "
                        + $"0x{mappedNotification.Notification.EventId:X2} "
                        + "uses an unsupported runtime event-value encoding.")
            };

        _eventRouter.Publish(
            mappedNotification.Mapping.InstrumentId,
            mappedNotification.Mapping.EventPath,
            _timeProvider.GetUtcNow().ToUniversalTime(),
            value);
    }
}