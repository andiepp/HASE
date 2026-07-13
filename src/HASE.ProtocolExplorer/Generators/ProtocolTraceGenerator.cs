using Hase.Protocol;
using Hase.ProtocolExplorer.Formatting;
using Hase.ProtocolExplorer.Formatting.Protocol;
using Hase.ProtocolExplorer.Tracing.Model;

namespace Hase.ProtocolExplorer.Generators;

internal sealed class ProtocolTraceGenerator
{
    private readonly BinaryProtocolPayloadCodec _codec =
        new();

    private readonly ProtocolMessageFormatter
        _messageFormatter =
        new();

    private readonly HexDumpFormatter
        _hexDumpFormatter =
        new();

    public TraceDocument Generate(
        ProtocolMessage message)
    {
        ArgumentNullException.ThrowIfNull(
            message);

        ProtocolEnvelope envelope =
            _codec.Encode(
                message);

        ProtocolMessage decodedMessage =
            _codec.Decode(
                envelope);

        TraceDocument trace =
            new();

        trace.AddSection(
            "Scenario",
            GetScenarioName(message));

        trace.AddSection(
            "Protocol Message",
            _messageFormatter
                .Format(message)
                .ToArray());

        trace.AddSection(
            "Envelope",
            $"Version      : {envelope.Version}",
            $"Role         : {envelope.Role}",
            $"Message Type : {envelope.MessageType}",
            $"Correlation  : {envelope.CorrelationId}",
            $"Payload Size : {envelope.PayloadLength} bytes");

        trace.AddSection(
            "Payload Bytes",
            _hexDumpFormatter
                .Format(envelope.Payload.Span)
                .ToArray());

        trace.AddSection(
            "Decoded Message",
            _messageFormatter
                .Format(decodedMessage)
                .ToArray());

        return trace;
    }

    private static string GetScenarioName(
        ProtocolMessage message)
    {
        return message switch
        {
            DiscoverRequest =>
                "discover",

            ReadPropertyRequest =>
                "read",

            WritePropertyRequest =>
                "write",

            ExecuteCommandRequest =>
                "command",

            EventNotification =>
                "event",

            _ =>
                message.MessageType.ToString()
        };
    }
}
