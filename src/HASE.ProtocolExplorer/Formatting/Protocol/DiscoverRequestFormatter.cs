using Hase.Protocol;

namespace Hase.ProtocolExplorer.Formatting.Protocol;

internal sealed class DiscoverRequestFormatter
    : IProtocolMessageFormatter
{
    public bool CanFormat(
        ProtocolMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return message is DiscoverRequest;
    }

    public IReadOnlyList<string> Format(
        ProtocolMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message is not DiscoverRequest request)
        {
            throw new ArgumentException(
                $"The formatter cannot format message type " +
                $"'{message.GetType().Name}'.",
                nameof(message));
        }

        return
        [
            $"Type          : {request.MessageType}",
            $"Role          : {request.Role}",
            $"Version       : {request.Version}",
            $"CorrelationId : {request.CorrelationId}",
            "",
            "Fields",
            "",
            "(none)"
        ];
    }
}