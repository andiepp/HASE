using Hase.Protocol;
using Hase.ProtocolExplorer.Formatting;
using Hase.ProtocolExplorer.Formatting.Payload;
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

    private readonly ReadPropertyRequestPayloadFormatter
        _readPropertyPayloadFormatter =
        new();

    private readonly DiscoverResponsePayloadFormatter
        _discoverResponsePayloadFormatter =
        new();

    private readonly WritePropertyRequestPayloadFormatter
        _writePropertyPayloadFormatter =
        new();

    private readonly ExecuteCommandRequestPayloadFormatter
        _executeCommandPayloadFormatter =
        new();

    private readonly EventNotificationPayloadFormatter
        _eventNotificationPayloadFormatter =
        new();

    private readonly ReadPropertyResponsePayloadFormatter
        _readPropertyResponsePayloadFormatter =
        new();

    private readonly WritePropertyResponsePayloadFormatter
        _writePropertyResponsePayloadFormatter =
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
            GetScenarioName(
                message));

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

        IReadOnlyList<string> payloadStructure =
            FormatPayloadStructure(
                message,
                envelope.Payload);

        if (payloadStructure.Count > 0)
        {
            trace.AddSection(
                "Payload Structure",
                payloadStructure.ToArray());
        }

        trace.AddSection(
            "Decoded Message",
            _messageFormatter
                .Format(decodedMessage)
                .ToArray());

        return trace;
    }

    private IReadOnlyList<string> FormatPayloadStructure(
        ProtocolMessage message,
        ReadOnlyMemory<byte> payload)
    {
        IReadOnlyList<PayloadField> fields =
            message switch
            {
                ReadPropertyRequest request =>
                    _readPropertyPayloadFormatter
                        .Format(
                            request,
                            payload),

                DiscoverResponse response =>
                    _discoverResponsePayloadFormatter
                        .Format(
                            response,
                            payload),

                WritePropertyRequest request =>
                    _writePropertyPayloadFormatter
                        .Format(
                            request,
                            payload),

                ExecuteCommandRequest request =>
                    _executeCommandPayloadFormatter
                        .Format(
                            request,
                            payload),

                EventNotification notification =>
                    _eventNotificationPayloadFormatter
                        .Format(
                            notification,
                            payload),

                ReadPropertyResponse response =>
                    _readPropertyResponsePayloadFormatter
                        .Format(
                            response,
                            payload),

                WritePropertyResponse response =>
                    _writePropertyResponsePayloadFormatter
                        .Format(
                            response,
                            payload),

                _ =>
                    []
            };

        if (fields.Count == 0)
        {
            return [];
        }

        List<string> lines =
        [
            "Offset  Length  Bytes                                      Description",
            "------  ------  -----------------------------------------  ------------------------------"
        ];

        foreach (PayloadField field in fields)
        {
            string bytes =
                FormatBytes(
                    field.Bytes.Span);

            lines.Add(
                $"{field.Offset:X4}    " +
                $"{field.Length,6}  " +
                $"{bytes,-41}  " +
                $"{field.Description}");
        }

        return lines;
    }

    private static string FormatBytes(
        ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return "<empty>";
        }

        return string.Join(
            " ",
            bytes
                .ToArray()
                .Select(
                    value =>
                        value.ToString("X2")));
    }

    private static string GetScenarioName(
        ProtocolMessage message)
    {
        return message switch
        {
            DiscoverRequest =>
                "discover",

            DiscoverResponse =>
                "discover-response",

            ReadPropertyRequest =>
                "read",

            ReadPropertyResponse =>
                "read-response",

            WritePropertyRequest =>
                "write",

            WritePropertyResponse =>
                "write-response",

            ExecuteCommandRequest =>
                "command",

            EventNotification =>
                "event",

            _ =>
                message.MessageType.ToString()
        };
    }
}