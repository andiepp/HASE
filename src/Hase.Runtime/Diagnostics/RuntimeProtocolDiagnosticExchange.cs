using System.Globalization;

namespace Hase.Runtime.Diagnostics;

/// <summary>
/// Publishes one correlated logical Protocol exchange without payload content.
/// </summary>
public sealed class RuntimeProtocolDiagnosticExchange
{
    private readonly RuntimeDiagnosticPublisher diagnostics;
    private readonly string endpointId;
    private readonly string protocolFamily;
    private readonly string requestMessageKind;
    private readonly string correlationId;
    private readonly int requestPayloadLength;
    private readonly TimeProvider timeProvider;
    private readonly long startedTimestamp;
    private int completed;

    public RuntimeProtocolDiagnosticExchange(
        RuntimeDiagnosticPublisher diagnostics,
        string endpointId,
        string protocolFamily,
        string requestMessageKind,
        string correlationId,
        int requestPayloadLength)
        : this(
            diagnostics,
            endpointId,
            protocolFamily,
            requestMessageKind,
            correlationId,
            requestPayloadLength,
            TimeProvider.System)
    {
    }

    internal RuntimeProtocolDiagnosticExchange(
        RuntimeDiagnosticPublisher diagnostics,
        string endpointId,
        string protocolFamily,
        string requestMessageKind,
        string correlationId,
        int requestPayloadLength,
        TimeProvider timeProvider)
    {
        this.diagnostics =
            diagnostics
            ?? throw new ArgumentNullException(
                nameof(diagnostics));

        ArgumentNullException.ThrowIfNull(
            timeProvider);

        this.endpointId =
            NormalizeRequired(
                endpointId,
                nameof(endpointId));

        this.protocolFamily =
            NormalizeRequired(
                protocolFamily,
                nameof(protocolFamily));

        this.requestMessageKind =
            NormalizeRequired(
                requestMessageKind,
                nameof(requestMessageKind));

        this.correlationId =
            NormalizeRequired(
                correlationId,
                nameof(correlationId));

        ValidatePayloadLength(
            requestPayloadLength,
            nameof(requestPayloadLength));

        this.requestPayloadLength =
            requestPayloadLength;

        this.timeProvider =
            timeProvider;

        startedTimestamp =
            timeProvider.GetTimestamp();

        Publish(
            "ProtocolRequestSent",
            RuntimeDiagnosticSeverity.Trace,
            RuntimeDiagnosticDirection.Outbound,
            requestMessageKind,
            requestPayloadLength);
    }

    public void Complete(
        string terminalMessageKind,
        int terminalPayloadLength,
        RuntimeDiagnosticDirection direction,
        RuntimeDiagnosticOutcome outcome)
    {
        string normalizedMessageKind =
            NormalizeRequired(
                terminalMessageKind,
                nameof(terminalMessageKind));

        ValidatePayloadLength(
            terminalPayloadLength,
            nameof(terminalPayloadLength));

        ValidateDirection(
            direction);

        ValidateOutcome(
            outcome);

        if (Interlocked.CompareExchange(
                ref completed,
                1,
                0) != 0)
        {
            return;
        }

        bool succeeded =
            outcome ==
            RuntimeDiagnosticOutcome.Succeeded;

        Publish(
            succeeded
                ? "ProtocolResponseReceived"
                : "ProtocolExchangeFailed",
            succeeded
                ? RuntimeDiagnosticSeverity.Trace
                : RuntimeDiagnosticSeverity.Warning,
            direction,
            normalizedMessageKind,
            terminalPayloadLength,
            timeProvider.GetElapsedTime(
                startedTimestamp),
            outcome);
    }

    public static void PublishNotification(
        RuntimeDiagnosticPublisher diagnostics,
        string endpointId,
        string protocolFamily,
        string messageKind,
        string correlationId,
        int payloadLength)
    {
        ArgumentNullException.ThrowIfNull(
            diagnostics);

        string normalizedEndpointId =
            NormalizeRequired(
                endpointId,
                nameof(endpointId));

        string normalizedProtocolFamily =
            NormalizeRequired(
                protocolFamily,
                nameof(protocolFamily));

        string normalizedMessageKind =
            NormalizeRequired(
                messageKind,
                nameof(messageKind));

        string normalizedCorrelationId =
            NormalizeRequired(
                correlationId,
                nameof(correlationId));

        ValidatePayloadLength(
            payloadLength,
            nameof(payloadLength));

        diagnostics.Publish(
            RuntimeDiagnosticLevel.Protocol,
            () =>
                CreateEvent(
                    normalizedEndpointId,
                    normalizedProtocolFamily,
                    normalizedMessageKind,
                    normalizedCorrelationId,
                    payloadLength,
                    "ProtocolNotificationReceived",
                    RuntimeDiagnosticSeverity.Trace,
                    RuntimeDiagnosticDirection.Inbound));
    }

    private void Publish(
        string eventName,
        RuntimeDiagnosticSeverity severity,
        RuntimeDiagnosticDirection direction,
        string messageKind,
        int payloadLength,
        TimeSpan? duration = null,
        RuntimeDiagnosticOutcome? outcome = null)
    {
        diagnostics.Publish(
            RuntimeDiagnosticLevel.Protocol,
            () =>
                CreateEvent(
                    endpointId,
                    protocolFamily,
                    messageKind,
                    correlationId,
                    payloadLength,
                    eventName,
                    severity,
                    direction,
                    duration,
                    outcome));
    }

    private static RuntimeDiagnosticEvent CreateEvent(
        string endpointId,
        string protocolFamily,
        string messageKind,
        string correlationId,
        int payloadLength,
        string eventName,
        RuntimeDiagnosticSeverity severity,
        RuntimeDiagnosticDirection direction,
        TimeSpan? duration = null,
        RuntimeDiagnosticOutcome? outcome = null)
    {
        return new RuntimeDiagnosticEvent(
            RuntimeDiagnosticLevel.Protocol,
            RuntimeDiagnosticCategory.ProtocolExchange,
            eventName,
            severity,
            endpointId,
            direction: direction,
            duration: duration,
            outcome: outcome,
            details:
                new Dictionary<string, string>
                {
                    ["protocolFamily"] =
                        protocolFamily,
                    ["messageKind"] =
                        messageKind,
                    ["correlationId"] =
                        correlationId,
                    ["payloadLength"] =
                        payloadLength.ToString(
                            CultureInfo.InvariantCulture)
                });
    }

    private static string NormalizeRequired(
        string value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            throw new ArgumentException(
                "Value must not be empty.",
                parameterName);
        }

        return value.Trim();
    }

    private static void ValidatePayloadLength(
        int payloadLength,
        string parameterName)
    {
        if (payloadLength < 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                payloadLength,
                "Payload length must not be negative.");
        }
    }

    private static void ValidateDirection(
        RuntimeDiagnosticDirection direction)
    {
        if (!Enum.IsDefined(
                direction))
        {
            throw new ArgumentOutOfRangeException(
                nameof(direction),
                direction,
                "Value is not defined.");
        }
    }

    private static void ValidateOutcome(
        RuntimeDiagnosticOutcome outcome)
    {
        if (!Enum.IsDefined(
                outcome))
        {
            throw new ArgumentOutOfRangeException(
                nameof(outcome),
                outcome,
                "Value is not defined.");
        }
    }
}
