using Hase.Protocol;

namespace Hase.ProtocolExplorer.Formatting.Protocol;

internal sealed class ReadEndpointDescriptorResponseFormatter
    : IProtocolMessageFormatter
{
    public bool CanFormat(
        ProtocolMessage message)
    {
        ArgumentNullException.ThrowIfNull(
            message);

        return message is ReadEndpointDescriptorResponse;
    }

    public IReadOnlyList<string> Format(
        ProtocolMessage message)
    {
        ArgumentNullException.ThrowIfNull(
            message);

        if (message is not
            ReadEndpointDescriptorResponse response)
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

        if (response.Descriptor is null)
        {
            lines.Add(
                "Descriptor    : <null>");

            return lines;
        }

        lines.Add(
            $"EndpointId    : {response.Descriptor.Id}");

        lines.Add(
            $"DisplayName   : " +
            $"{FormatOptionalText(response.Descriptor.Metadata.DisplayName)}");

        lines.Add(
            $"Description   : " +
            $"{FormatOptionalText(response.Descriptor.Metadata.Description)}");

        lines.Add(
            $"Instruments   : " +
            $"{response.Descriptor.Instruments.Count}");

        for (int index = 0;
            index < response.Descriptor.Instruments.Count;
            index++)
        {
            var instrument =
                response.Descriptor.Instruments[index];

            lines.Add(
                $"  [{index}] Id   : {instrument.Id}");

            lines.Add(
                $"      Name : {instrument.Name}");

            lines.Add(
                $"      Kind : {instrument.Kind.Name}");

            lines.Add(
                $"      Properties: " +
                $"{instrument.Interface.Properties.Count}");

            lines.Add(
                $"      Commands  : " +
                $"{instrument.Interface.Commands.Count}");

            lines.Add(
                $"      Events    : " +
                $"{instrument.Interface.Events.Count}");
        }

        return lines;
    }

    private static string FormatOptionalText(
        string? value)
    {
        return value is null
            ? "<null>"
            : $"\"{value}\"";
    }
}