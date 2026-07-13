using Hase.Protocol;

namespace Hase.ProtocolExplorer.Formatting.Protocol;

internal sealed class ProtocolMessageFormatter
{
    private readonly IReadOnlyList<IProtocolMessageFormatter>
        _formatters =
        [
            new DiscoverRequestFormatter(),
            new DiscoverResponseFormatter(),
            new ReadPropertyRequestFormatter(),
            new WritePropertyRequestFormatter(),
            new ExecuteCommandRequestFormatter(),
            new EventNotificationFormatter()
        ];

    public IReadOnlyList<string> Format(
        ProtocolMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        foreach (IProtocolMessageFormatter formatter
            in _formatters)
        {
            if (formatter.CanFormat(message))
            {
                return formatter.Format(message);
            }
        }

        return
        [
            $"Type          : {message.MessageType}",
            $"Role          : {message.Role}",
            $"Version       : {message.Version}",
            $"CorrelationId : {message.CorrelationId}",
            "",
            "Fields",
            "",
            "(formatter not implemented)"
        ];
    }
}
