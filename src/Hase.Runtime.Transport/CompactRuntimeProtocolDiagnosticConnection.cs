using System.Globalization;
using Hase.CompactProtocol;
using Hase.Runtime.Diagnostics;
using Hase.Transport;

namespace Hase.Runtime.Transport;

/// <summary>
/// Decorates one compact runtime protocol connection with payload-free
/// Protocol-level diagnostics.
/// </summary>
internal class CompactRuntimeProtocolDiagnosticConnection
    : ICompactSerialProtocolConnection
{
    private const string ProtocolFamily =
        "CompactSerialProtocolV1";

    private readonly ICompactSerialProtocolConnection inner;
    private readonly RuntimeDiagnosticPublisher diagnostics;
    private readonly string endpointId;
    private readonly CompactProtocolNotificationDiagnosticObserver?
        notificationObserver;

    private int disposed;

    private CompactRuntimeProtocolDiagnosticConnection(
        ICompactSerialProtocolConnection inner,
        string endpointId,
        RuntimeDiagnosticPublisher diagnostics,
        bool observeNotifications)
    {
        this.inner =
            inner;

        this.endpointId =
            endpointId;

        this.diagnostics =
            diagnostics;

        if (observeNotifications)
        {
            notificationObserver =
                new CompactProtocolNotificationDiagnosticObserver(
                    endpointId,
                    diagnostics);

            inner.EventNotificationReceived +=
                notificationObserver.OnEventNotification;
        }
    }

    public static ICompactSerialProtocolConnection Create(
        ICompactSerialProtocolConnection inner,
        string endpointId,
        RuntimeDiagnosticPublisher diagnostics,
        bool observeNotifications = false)
    {
        ArgumentNullException.ThrowIfNull(
            inner);

        ArgumentNullException.ThrowIfNull(
            diagnostics);

        if (string.IsNullOrWhiteSpace(
                endpointId))
        {
            throw new ArgumentException(
                "Endpoint identity must not be empty.",
                nameof(endpointId));
        }

        string normalizedEndpointId =
            endpointId.Trim();

        if (inner is ITransportExchangeTraceSource traceSource)
        {
            return new TraceConnection(
                inner,
                normalizedEndpointId,
                diagnostics,
                observeNotifications,
                traceSource);
        }

        return new CompactRuntimeProtocolDiagnosticConnection(
            inner,
            normalizedEndpointId,
            diagnostics,
            observeNotifications);
    }

    public event EventHandler<TransportConnectionStateChangedEventArgs>?
        StateChanged
    {
        add =>
            inner.StateChanged += value;

        remove =>
            inner.StateChanged -= value;
    }

    public event Action<CompactEventNotification>?
        EventNotificationReceived
    {
        add =>
            inner.EventNotificationReceived += value;

        remove =>
            inner.EventNotificationReceived -= value;
    }

    public TransportConnectionState State =>
        inner.State;

    public async Task<CompactSerialFrame> ExchangeAsync(
        CompactSerialFrame request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        RuntimeProtocolDiagnosticExchange? exchange =
            CreateExchange(
                request);

        try
        {
            CompactSerialFrame response =
                await inner
                    .ExchangeAsync(
                        request,
                        cancellationToken)
                    .ConfigureAwait(
                        false);

            exchange?.Complete(
                GetMessageKind(
                    response.MessageType),
                response.Payload.Length,
                RuntimeDiagnosticDirection.Inbound,
                RuntimeDiagnosticOutcome.Succeeded);

            return response;
        }
        catch (TimeoutException)
        {
            CompleteFailed(
                exchange,
                request,
                RuntimeDiagnosticOutcome.TimedOut);

            throw;
        }
        catch (OperationCanceledException)
        {
            CompleteFailed(
                exchange,
                request,
                RuntimeDiagnosticOutcome.Cancelled);

            throw;
        }
        catch
        {
            CompleteFailed(
                exchange,
                request,
                RuntimeDiagnosticOutcome.Failed);

            throw;
        }
    }

    public void Invalidate()
    {
        inner.Invalidate();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(
                ref disposed,
                1) != 0)
        {
            return;
        }

        if (notificationObserver is not null)
        {
            inner.EventNotificationReceived -=
                notificationObserver.OnEventNotification;
        }

        await inner.DisposeAsync();
    }

    private RuntimeProtocolDiagnosticExchange? CreateExchange(
        CompactSerialFrame request)
    {
        if (!diagnostics.IsEnabled(
                RuntimeDiagnosticLevel.Protocol))
        {
            return null;
        }

        return new RuntimeProtocolDiagnosticExchange(
            diagnostics,
            endpointId,
            ProtocolFamily,
            GetMessageKind(
                request.MessageType),
            request.CorrelationId.ToString(
                CultureInfo.InvariantCulture),
            request.Payload.Length);
    }

    private static void CompleteFailed(
        RuntimeProtocolDiagnosticExchange? exchange,
        CompactSerialFrame request,
        RuntimeDiagnosticOutcome outcome)
    {
        exchange?.Complete(
            GetMessageKind(
                request.MessageType),
            request.Payload.Length,
            RuntimeDiagnosticDirection.Outbound,
            outcome);
    }

    internal static string GetMessageKind(
        byte messageType)
    {
        return Enum.IsDefined(
            typeof(CompactSerialMessageType),
            messageType)
            ? ((CompactSerialMessageType)messageType).ToString()
            : $"0x{messageType:X2}";
    }

    private sealed class TraceConnection
        : CompactRuntimeProtocolDiagnosticConnection,
          ITransportExchangeTraceSource
    {
        private readonly ITransportExchangeTraceSource source;

        public TraceConnection(
            ICompactSerialProtocolConnection inner,
            string endpointId,
            RuntimeDiagnosticPublisher diagnostics,
            bool observeNotifications,
            ITransportExchangeTraceSource source)
            : base(
                inner,
                endpointId,
                diagnostics,
                observeNotifications)
        {
            this.source =
                source;
        }

        public void SubscribeTrace(
            ITransportExchangeTraceObserver observer)
        {
            source.SubscribeTrace(
                observer);
        }

        public void UnsubscribeTrace(
            ITransportExchangeTraceObserver observer)
        {
            source.UnsubscribeTrace(
                observer);
        }
    }
}
