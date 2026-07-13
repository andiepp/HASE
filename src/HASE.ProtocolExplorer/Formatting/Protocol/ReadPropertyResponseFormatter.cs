using System.Globalization;
using Hase.Protocol;

namespace Hase.ProtocolExplorer.Formatting.Protocol;

internal sealed class ReadPropertyResponseFormatter
    : IProtocolMessageFormatter
{
    public bool CanFormat(
        ProtocolMessage message)
    {
        ArgumentNullException.ThrowIfNull(
            message);

        return message is ReadPropertyResponse;
    }

    public IReadOnlyList<string> Format(
        ProtocolMessage message)
    {
        ArgumentNullException.ThrowIfNull(
            message);

        if (message is not ReadPropertyResponse response)
        {
            throw new ArgumentException(
                $"The formatter cannot format message type " +
                $"'{message.GetType().Name}'.",
                nameof(message));
        }

        List<string> lines =
        [
            $"Type          : {response.MessageType}",
            $"Role          : {response.Role}",
            $"Version       : {response.Version}",
            $"CorrelationId : {response.CorrelationId}",
            "",
            "Fields",
            "",
            $"Result Code   : {response.Result.Code}",
            $"Result Message: {FormatOptionalText(response.Result.Message)}"
        ];

        if (response.PropertyValue is null)
        {
            lines.Add(
                "PropertyValue : <null>");

            return lines;
        }

        lines.Add(
            $"Value         : {FormatValue(response.PropertyValue.Value)}");

        lines.Add(
            $"TimestampUtc  : {response.PropertyValue.TimestampUtc:O}");

        lines.Add(
            $"Quality       : {response.PropertyValue.Quality}");

        return lines;
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