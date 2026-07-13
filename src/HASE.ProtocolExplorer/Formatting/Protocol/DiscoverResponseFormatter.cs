using Hase.Core.Domain.Identity;
using Hase.Protocol;

namespace Hase.ProtocolExplorer.Formatting.Protocol;

internal sealed class DiscoverResponseFormatter
    : IProtocolMessageFormatter
{
    public bool CanFormat(
        ProtocolMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return message is DiscoverResponse;
    }

    public IReadOnlyList<string> Format(
        ProtocolMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message is not DiscoverResponse response)
        {
            throw new ArgumentException(
                $"The formatter cannot format message type '{message.GetType().Name}'.",
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
            $"EndpointId    : {response.EndpointId}",
            "",
            "InstrumentIds"
        ];

        if (response.InstrumentIds.Count == 0)
        {
            lines.Add("  (none)");
        }
        else
        {
            foreach (InstrumentId instrumentId in response.InstrumentIds)
            {
                lines.Add($"  {instrumentId}");
            }
        }

        return lines;
    }
}