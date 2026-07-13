using Hase.Protocol;

namespace Hase.ProtocolExplorer.Formatting.Protocol;

internal sealed class ExecuteCommandRequestFormatter
    : IProtocolMessageFormatter
{
    public bool CanFormat(
        ProtocolMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return message is ExecuteCommandRequest;
    }

    public IReadOnlyList<string> Format(
        ProtocolMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message is not ExecuteCommandRequest request)
        {
            throw new ArgumentException(
                $"The formatter cannot format message type '{message.GetType().Name}'.",
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
            $"CommandPath   : {request.CommandPath}",
            $"Argument      : {FormatValue(request.Argument)}"
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