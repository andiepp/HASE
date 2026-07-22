namespace Hase.CompactProtocol;

/// <summary>
/// Resolves decoded compact event notifications through one validated
/// descriptor-side compact event map.
/// </summary>
internal sealed class CompactEventNotificationResolver
{
    private readonly CompactEventMap _eventMap;

    public CompactEventNotificationResolver(
        CompactEventMap eventMap)
    {
        _eventMap =
            eventMap
            ?? throw new ArgumentNullException(
                nameof(eventMap));
    }

    public CompactMappedEventNotification Resolve(
        CompactEventNotification notification)
    {
        ArgumentNullException.ThrowIfNull(
            notification);

        CompactEventMapping mapping =
            _eventMap.Find(
                notification.EventId)
            ?? throw new InvalidDataException(
                $"Compact event identifier 0x{notification.EventId:X2} "
                + "is not mapped by the selected endpoint descriptor.");

        ValidateValue(
            notification,
            mapping);

        return new CompactMappedEventNotification(
            notification,
            mapping);
    }

    private static void ValidateValue(
        CompactEventNotification notification,
        CompactEventMapping mapping)
    {
        switch (mapping.Encoding)
        {
            case CompactEventValueEncoding.None:
                if (!notification.Value.IsEmpty)
                {
                    throw new InvalidDataException(
                        $"Compact event identifier "
                        + $"0x{notification.EventId:X2} is mapped as "
                        + $"{CompactEventValueEncoding.None} but contains "
                        + $"{notification.Value.Length} value byte(s).");
                }

                break;

            default:
                throw new InvalidDataException(
                    $"Compact event identifier "
                    + $"0x{notification.EventId:X2} uses unsupported event "
                    + $"value encoding 0x{(byte)mapping.Encoding:X2}.");
        }
    }
}