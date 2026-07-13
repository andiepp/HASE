using System.Globalization;
using Hase.Protocol;

namespace Hase.ProtocolExplorer.Formatting.Protocol;

internal sealed class ExecuteCommandResponseFormatter
    : IProtocolMessageFormatter
{
    public bool CanFormat(
        ProtocolMessage message)
    {
        ArgumentNullException.ThrowIfNull(
            message);

        return message is ExecuteCommandResponse;
    }

    public IReadOnlyList<string> Format(
        ProtocolMessage message)
    {
        ArgumentNullException.ThrowIfNull(
            message);

        if (message is not ExecuteCommandResponse response)
        {
            throw new ArgumentException(
                $"The formatter cannot format message type " +
                $"'{message.GetType().Name}'.",
                nameof(message));
        }

        return
        [
            $"Type          : {response.MessageType}",
            $"Role          : {response.Role}",
            $"Version       : {response.Version}",
            $"CorrelationId : {response.CorrelationId}",
            "",
            "Fields",
            "",
            $"Result Code   : {response.Result.Code}",
            $"Result Message: {FormatOptionalText(response.Result.Message)}",
            $"Return Value  : {FormatValue(response.ReturnValue)}"
        ];
    }

    private static string FormatOptionalText(
        string? value)
    {
        return value is null
            ? "<null>"
            : $"\"{value}\"";
    }

    private static string FormatValue(
        object? value)
    {
        return value switch
        {
            null =>
                "<null>",

            string text =>
                $"\"{text}\"",

            double doubleValue =>
                doubleValue.ToString(
                    CultureInfo.InvariantCulture),

            float floatValue =>
                floatValue.ToString(
                    CultureInfo.InvariantCulture),

            IFormattable formattable =>
                formattable.ToString(
                    null,
                    CultureInfo.InvariantCulture)
                ?? "<null>",

            _ =>
                value.ToString() ?? "<null>"
        };
    }
}