using System.Globalization;
using Hase.Runtime.Diagnostics;

namespace Hase.Scpi.Kel103.Hosting;

/// <summary>
/// Maps transport-independent SCPI observations to the established runtime
/// Protocol and Bytes diagnostic records without disclosing payload content at
/// Protocol level.
/// </summary>
internal sealed class Kel103ScpiDiagnosticObserver : IScpiDiagnosticObserver
{
    internal const string ProtocolFamily = "ScpiText";

    private readonly object gate = new();
    private readonly RuntimeDiagnosticPublisher diagnostics;
    private readonly RuntimeTransportByteDiagnosticPublisher byteDiagnostics;
    private readonly string endpointId;
    private readonly Dictionary<Guid, ExchangeState> exchanges = new();

    public Kel103ScpiDiagnosticObserver(
        string endpointId,
        RuntimeDiagnosticPublisher diagnostics)
    {
        if (string.IsNullOrWhiteSpace(endpointId))
        {
            throw new ArgumentException(
                "Endpoint identity must not be empty.",
                nameof(endpointId));
        }

        this.diagnostics =
            diagnostics
            ?? throw new ArgumentNullException(nameof(diagnostics));
        this.endpointId = endpointId.Trim();
        byteDiagnostics = new RuntimeTransportByteDiagnosticPublisher(
            diagnostics,
            this.endpointId,
            ProtocolFamily);
    }

    public void Observe(ScpiDiagnosticEvent diagnosticEvent)
    {
        ArgumentNullException.ThrowIfNull(diagnosticEvent);

        lock (gate)
        {
            switch (diagnosticEvent)
            {
                case ScpiDiagnosticExchangeStarted started:
                    exchanges.TryAdd(
                        started.ExchangeId,
                        new ExchangeState(started.ExchangeKind));
                    break;

                case ScpiDiagnosticBytesObserved bytes:
                    ObserveBytes(bytes);
                    break;

                case ScpiDiagnosticExchangeCompleted completed:
                    Complete(completed);
                    break;

                case ScpiDiagnosticExchangeFailed failed:
                    Fail(failed);
                    break;
            }
        }
    }

    private void ObserveBytes(ScpiDiagnosticBytesObserved observation)
    {
        ExchangeState state = GetOrCreateState(observation);

        if (observation.Direction == ScpiDiagnosticDirection.Transmit)
        {
            state.TransmittedByteCount += observation.ByteCount;
            PublishRequestIfRequired(observation.ExchangeId, state);
        }
        else
        {
            state.ReceivedByteCount += observation.ByteCount;
        }

        byteDiagnostics.Publish(
            MapDirection(observation.Direction),
            CorrelationId(observation.ExchangeId),
            () => observation.ToArray());
    }

    private void Complete(ScpiDiagnosticExchangeCompleted observation)
    {
        if (!exchanges.Remove(observation.ExchangeId, out ExchangeState? state))
        {
            return;
        }

        PublishRequestIfRequired(observation.ExchangeId, state);
        PublishTerminal(
            observation.ExchangeId,
            state,
            observation.Duration,
            RuntimeDiagnosticOutcome.Succeeded,
            failureKind: null,
            executionMayHaveOccurred: false,
            scpiOutcome: observation.Outcome);
    }

    private void Fail(ScpiDiagnosticExchangeFailed observation)
    {
        if (!exchanges.Remove(observation.ExchangeId, out ExchangeState? state))
        {
            return;
        }

        PublishRequestIfRequired(observation.ExchangeId, state);
        PublishTerminal(
            observation.ExchangeId,
            state,
            observation.Duration,
            MapOutcome(observation.Outcome),
            observation.FailureKind,
            observation.ExecutionMayHaveOccurred,
            observation.Outcome);
    }

    private ExchangeState GetOrCreateState(ScpiDiagnosticEvent observation)
    {
        if (exchanges.TryGetValue(observation.ExchangeId, out ExchangeState? state))
        {
            return state;
        }

        state = new ExchangeState(observation.ExchangeKind);
        exchanges.Add(observation.ExchangeId, state);
        return state;
    }

    private void PublishRequestIfRequired(Guid exchangeId, ExchangeState state)
    {
        if (state.RequestPublished || state.TransmittedByteCount == 0)
        {
            return;
        }

        state.RequestPublished = true;
        PublishProtocol(
            "ProtocolRequestSent",
            RuntimeDiagnosticSeverity.Trace,
            RuntimeDiagnosticDirection.Outbound,
            exchangeId,
            state.ExchangeKind,
            state.TransmittedByteCount);
    }

    private void PublishTerminal(
        Guid exchangeId,
        ExchangeState state,
        TimeSpan duration,
        RuntimeDiagnosticOutcome outcome,
        ScpiDiagnosticFailureKind? failureKind,
        bool executionMayHaveOccurred,
        ScpiDiagnosticOutcome scpiOutcome)
    {
        bool succeeded = outcome == RuntimeDiagnosticOutcome.Succeeded;
        RuntimeDiagnosticDirection direction =
            state.ReceivedByteCount > 0
                ? RuntimeDiagnosticDirection.Inbound
                : RuntimeDiagnosticDirection.Outbound;
        int payloadLength =
            state.ReceivedByteCount > 0
                ? state.ReceivedByteCount
                : 0;

        var details = CreateDetails(
            exchangeId,
            state.ExchangeKind,
            payloadLength);
        details["scpiOutcome"] = scpiOutcome.ToString();

        if (failureKind is not null)
        {
            details["failureKind"] = failureKind.Value.ToString();
            details["executionMayHaveOccurred"] =
                executionMayHaveOccurred.ToString(CultureInfo.InvariantCulture);
        }

        diagnostics.Publish(
            RuntimeDiagnosticLevel.Protocol,
            () => new RuntimeDiagnosticEvent(
                RuntimeDiagnosticLevel.Protocol,
                RuntimeDiagnosticCategory.ProtocolExchange,
                succeeded
                    ? "ProtocolResponseReceived"
                    : "ProtocolExchangeFailed",
                succeeded
                    ? RuntimeDiagnosticSeverity.Trace
                    : RuntimeDiagnosticSeverity.Warning,
                endpointId,
                direction: direction,
                duration: duration,
                outcome: outcome,
                details: details));
    }

    private void PublishProtocol(
        string eventName,
        RuntimeDiagnosticSeverity severity,
        RuntimeDiagnosticDirection direction,
        Guid exchangeId,
        ScpiDiagnosticExchangeKind exchangeKind,
        int payloadLength)
    {
        diagnostics.Publish(
            RuntimeDiagnosticLevel.Protocol,
            () => new RuntimeDiagnosticEvent(
                RuntimeDiagnosticLevel.Protocol,
                RuntimeDiagnosticCategory.ProtocolExchange,
                eventName,
                severity,
                endpointId,
                direction: direction,
                details: CreateDetails(
                    exchangeId,
                    exchangeKind,
                    payloadLength)));
    }

    private static Dictionary<string, string> CreateDetails(
        Guid exchangeId,
        ScpiDiagnosticExchangeKind exchangeKind,
        int payloadLength)
    {
        return new Dictionary<string, string>
        {
            ["protocolFamily"] = ProtocolFamily,
            ["messageKind"] = MessageKind(exchangeKind),
            ["correlationId"] = CorrelationId(exchangeId),
            ["payloadLength"] = payloadLength.ToString(CultureInfo.InvariantCulture)
        };
    }

    private static string MessageKind(ScpiDiagnosticExchangeKind exchangeKind) =>
        exchangeKind switch
        {
            ScpiDiagnosticExchangeKind.Query => "ScpiQuery",
            ScpiDiagnosticExchangeKind.Command => "ScpiCommand",
            _ => throw new ArgumentOutOfRangeException(
                nameof(exchangeKind),
                exchangeKind,
                "SCPI exchange kind is not defined.")
        };

    private static RuntimeDiagnosticDirection MapDirection(
        ScpiDiagnosticDirection direction) =>
        direction switch
        {
            ScpiDiagnosticDirection.Transmit => RuntimeDiagnosticDirection.Outbound,
            ScpiDiagnosticDirection.Receive => RuntimeDiagnosticDirection.Inbound,
            _ => throw new ArgumentOutOfRangeException(
                nameof(direction),
                direction,
                "SCPI diagnostic direction is not defined.")
        };

    private static RuntimeDiagnosticOutcome MapOutcome(
        ScpiDiagnosticOutcome outcome) =>
        outcome switch
        {
            ScpiDiagnosticOutcome.Canceled => RuntimeDiagnosticOutcome.Cancelled,
            ScpiDiagnosticOutcome.TimedOut => RuntimeDiagnosticOutcome.TimedOut,
            ScpiDiagnosticOutcome.Failed or
            ScpiDiagnosticOutcome.Disposed or
            ScpiDiagnosticOutcome.Uncertain => RuntimeDiagnosticOutcome.Failed,
            _ => throw new ArgumentOutOfRangeException(
                nameof(outcome),
                outcome,
                "SCPI diagnostic outcome is not a failure outcome.")
        };

    private static string CorrelationId(Guid exchangeId) =>
        exchangeId.ToString("N");

    private sealed class ExchangeState(ScpiDiagnosticExchangeKind exchangeKind)
    {
        public ScpiDiagnosticExchangeKind ExchangeKind { get; } = exchangeKind;

        public int TransmittedByteCount { get; set; }

        public int ReceivedByteCount { get; set; }

        public bool RequestPublished { get; set; }
    }
}
