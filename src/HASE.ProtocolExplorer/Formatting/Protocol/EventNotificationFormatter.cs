using Hase.Protocol;

namespace Hase.ProtocolExplorer.Formatting.Protocol;

internal sealed class EventNotificationFormatter
    : IProtocolMessageFormatter
{
    public bool CanFormat(
        ProtocolMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return message is EventNotification;
    }

    public IReadOnlyList<string> Format(
        ProtocolMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message is not EventNotification notification)
        {
            throw new ArgumentException(
                $"The formatter cannot format message type '{message.GetType().Name}'.",
                nameof(message));
        }

        return
        [
            $"Type          : {notification.MessageType}",
            $"Role          : {notification.Role}",
            $"Version       : {notification.Version}",
            $"CorrelationId : {notification.CorrelationId}",
            "",
            "Fields",
            "",
            $"InstrumentId  : {notification.InstrumentId}",
            $"EventPath     : {notification.EventPath}",
            $"TimestampUtc  : {notification.TimestampUtc:O}",
            $"Value         : {FormatValue(notification.Value)}"
        ];
    }

    private static string FormatValue(
        object? value)
    {
        return value switch
        {
            null => "<null>",
            string text => $"\"{text}\"",
            _ => value.ToString() ?? "<null>"
        };
    }
}