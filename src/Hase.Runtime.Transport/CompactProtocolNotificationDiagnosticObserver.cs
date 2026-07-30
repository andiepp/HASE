using Hase.CompactProtocol;
using Hase.Runtime.Diagnostics;

namespace Hase.Runtime.Transport;

/// <summary>
/// Publishes payload-free Protocol-level metadata for compact notifications.
/// </summary>
internal sealed class CompactProtocolNotificationDiagnosticObserver
{
    private const string ProtocolFamily =
        "CompactSerialProtocolV1";

    private readonly string endpointId;
    private readonly RuntimeDiagnosticPublisher diagnostics;

    public CompactProtocolNotificationDiagnosticObserver(
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

    public void OnEventNotification(
        CompactEventNotification notification)
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
            CompactSerialMessageType.EventNotification.ToString(),
            "0",
            1 + notification.Value.Length);
    }
}
