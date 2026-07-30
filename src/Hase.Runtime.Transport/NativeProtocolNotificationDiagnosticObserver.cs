using Hase.Protocol;
using Hase.Runtime.Diagnostics;

namespace Hase.Runtime.Transport;

/// <summary>
/// Publishes payload-free Protocol-level metadata for native notifications.
/// </summary>
public sealed class NativeProtocolNotificationDiagnosticObserver
    : IProtocolNotificationObserver
{
    private const string ProtocolFamily =
        "NativeProtocolV1";

    private readonly string endpointId;
    private readonly RuntimeDiagnosticPublisher diagnostics;
    private readonly BinaryProtocolPayloadCodec payloadCodec =
        new();

    public NativeProtocolNotificationDiagnosticObserver(
        string endpointId,
        RuntimeDiagnosticPublisher diagnostics)
    {
        if (string.IsNullOrWhiteSpace(
                endpointId))
        {
            throw new ArgumentException(
                "Endpoint identity must not be empty.",
                nameof(endpointId));
        }

        this.endpointId =
            endpointId.Trim();

        this.diagnostics =
            diagnostics
            ?? throw new ArgumentNullException(
                nameof(diagnostics));
    }

    public void OnProtocolNotification(
        ProtocolMessage notification)
    {
        ArgumentNullException.ThrowIfNull(
            notification);

        if (!diagnostics.IsEnabled(
                RuntimeDiagnosticLevel.Protocol))
        {
            return;
        }

        RuntimeProtocolDiagnosticExchange.PublishNotification(
            diagnostics,
            endpointId,
            ProtocolFamily,
            notification.MessageType.ToString(),
            notification.CorrelationId.IsNone
                ? "none"
                : notification.CorrelationId.ToString(),
            GetPayloadLength(
                notification));
    }

    private int GetPayloadLength(
        ProtocolMessage notification)
    {
        try
        {
            return payloadCodec
                .Encode(
                    notification)
                .PayloadLength;
        }
        catch
        {
            return 0;
        }
    }
}
