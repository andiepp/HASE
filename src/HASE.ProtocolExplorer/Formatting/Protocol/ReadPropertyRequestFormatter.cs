using Hase.Protocol;

namespace Hase.ProtocolExplorer.Formatting.Protocol;

internal sealed class ReadPropertyRequestFormatter
    : IProtocolMessageFormatter
{
    public bool CanFormat(
        ProtocolMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return message is ReadPropertyRequest;
    }

    public IReadOnlyList<string> Format(
        ProtocolMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message is not ReadPropertyRequest request)
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
            $"InstrumentId  : {request.InstrumentId}",
            $"PropertyId    : {request.PropertyId}"
        ];
    }
}