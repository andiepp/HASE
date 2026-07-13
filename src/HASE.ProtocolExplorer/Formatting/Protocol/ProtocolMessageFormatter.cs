using Hase.Protocol;

namespace Hase.ProtocolExplorer.Formatting.Protocol;

internal sealed class ProtocolMessageFormatter
{
    private readonly IReadOnlyList<IProtocolMessageFormatter>
        _formatters =
        [
            new DiscoverRequestFormatter(),
            new ReadPropertyRequestFormatter(),
            new WritePropertyRequestFormatter()
        ];

    public IReadOnlyList<string> Format(
        ProtocolMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        foreach (IProtocolMessageFormatter formatter
            in _formatters)
        {
            if (!formatter.CanFormat(message))
            {
                continue;
            }

            return formatter.Format(message);
        }

        return CreateFallbackFormat(
            message);
    }

    private static IReadOnlyList<string>
        CreateFallbackFormat(
            ProtocolMessage message)
    {
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