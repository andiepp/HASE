using Hase.Protocol;
using Hase.Runtime.Diagnostics;
using Hase.Transport;

namespace Hase.Runtime.Transport;

/// <summary>
/// Decorates one native runtime protocol connection with payload-free
/// Protocol-level diagnostics.
/// </summary>
public class NativeRuntimeProtocolDiagnosticConnection
    : IRuntimeProtocolConnection
{
    private const string ProtocolFamily =
        "NativeProtocolV1";

    private readonly IRuntimeProtocolConnection inner;
    private readonly RuntimeDiagnosticPublisher diagnostics;
    private readonly string endpointId;
    private readonly BinaryProtocolPayloadCodec payloadCodec =
        new();

    private NativeRuntimeProtocolDiagnosticConnection(
        IRuntimeProtocolConnection inner,
        string endpointId,
        RuntimeDiagnosticPublisher diagnostics)
    {
        this.inner =
            inner;

        this.endpointId =
            endpointId;

        this.diagnostics =
            diagnostics;
    }

    public static IRuntimeProtocolConnection Create(
        IRuntimeProtocolConnection inner,
        string endpointId,
        RuntimeDiagnosticPublisher diagnostics)
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

        IRuntimeProtocolNotificationSource? notificationSource =
            inner as IRuntimeProtocolNotificationSource;

        ITransportExchangeTraceSource? traceSource =
            inner as ITransportExchangeTraceSource;

        if (notificationSource is not null &&
            traceSource is not null)
        {
            return new NotificationAndTraceConnection(
                inner,
                normalizedEndpointId,
                diagnostics,
                notificationSource,
                traceSource);
        }

        if (notificationSource is not null)
        {
            return new NotificationConnection(
                inner,
                normalizedEndpointId,
                diagnostics,
                notificationSource);
        }

        if (traceSource is not null)
        {
            return new TraceConnection(
                inner,
                normalizedEndpointId,
                diagnostics,
                traceSource);
        }

        return new NativeRuntimeProtocolDiagnosticConnection(
            inner,
            normalizedEndpointId,
            diagnostics);
    }

    public async Task<ProtocolMessage> SendAsync(
        ProtocolMessage request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        RuntimeProtocolDiagnosticExchange? exchange =
            CreateExchange(
                request);

        try
        {
            ProtocolMessage response =
                await inner
                    .SendAsync(
                        request,
                        cancellationToken)
                    .ConfigureAwait(
                        false);

            exchange?.Complete(
                response.MessageType.ToString(),
                GetPayloadLength(
                    response),
                RuntimeDiagnosticDirection.Inbound,
                SelectOutcome(
                    response));

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

    private RuntimeProtocolDiagnosticExchange? CreateExchange(
        ProtocolMessage request)
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
            request.MessageType.ToString(),
            request.CorrelationId.ToString(),
            GetPayloadLength(
                request));
    }

    private int GetPayloadLength(
        ProtocolMessage message)
    {
        try
        {
            return payloadCodec
                .Encode(
                    message)
                .PayloadLength;
        }
        catch
        {
            return 0;
        }
    }

    private static RuntimeDiagnosticOutcome SelectOutcome(
        ProtocolMessage response)
    {
        return response
                   is ProtocolResultResponse resultResponse &&
               !resultResponse.Result.IsSuccess
            ? RuntimeDiagnosticOutcome.Failed
            : RuntimeDiagnosticOutcome.Succeeded;
    }

    private void CompleteFailed(
        RuntimeProtocolDiagnosticExchange? exchange,
        ProtocolMessage request,
        RuntimeDiagnosticOutcome outcome)
    {
        exchange?.Complete(
            request.MessageType.ToString(),
            GetPayloadLength(
                request),
            RuntimeDiagnosticDirection.Outbound,
            outcome);
    }

    private sealed class NotificationConnection
        : NativeRuntimeProtocolDiagnosticConnection,
          IRuntimeProtocolNotificationSource
    {
        private readonly IRuntimeProtocolNotificationSource source;

        public NotificationConnection(
            IRuntimeProtocolConnection inner,
            string endpointId,
            RuntimeDiagnosticPublisher diagnostics,
            IRuntimeProtocolNotificationSource source)
            : base(
                inner,
                endpointId,
                diagnostics)
        {
            this.source =
                source;
        }

        public void SubscribeNotification(
            IProtocolNotificationObserver observer)
        {
            source.SubscribeNotification(
                observer);
        }

        public void UnsubscribeNotification(
            IProtocolNotificationObserver observer)
        {
            source.UnsubscribeNotification(
                observer);
        }
    }

    private sealed class TraceConnection
        : NativeRuntimeProtocolDiagnosticConnection,
          ITransportExchangeTraceSource
    {
        private readonly ITransportExchangeTraceSource source;

        public TraceConnection(
            IRuntimeProtocolConnection inner,
            string endpointId,
            RuntimeDiagnosticPublisher diagnostics,
            ITransportExchangeTraceSource source)
            : base(
                inner,
                endpointId,
                diagnostics)
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

    private sealed class NotificationAndTraceConnection
        : NativeRuntimeProtocolDiagnosticConnection,
          IRuntimeProtocolNotificationSource,
          ITransportExchangeTraceSource
    {
        private readonly IRuntimeProtocolNotificationSource
            notificationSource;

        private readonly ITransportExchangeTraceSource
            traceSource;

        public NotificationAndTraceConnection(
            IRuntimeProtocolConnection inner,
            string endpointId,
            RuntimeDiagnosticPublisher diagnostics,
            IRuntimeProtocolNotificationSource notificationSource,
            ITransportExchangeTraceSource traceSource)
            : base(
                inner,
                endpointId,
                diagnostics)
        {
            this.notificationSource =
                notificationSource;

            this.traceSource =
                traceSource;
        }

        public void SubscribeNotification(
            IProtocolNotificationObserver observer)
        {
            notificationSource.SubscribeNotification(
                observer);
        }

        public void UnsubscribeNotification(
            IProtocolNotificationObserver observer)
        {
            notificationSource.UnsubscribeNotification(
                observer);
        }

        public void SubscribeTrace(
            ITransportExchangeTraceObserver observer)
        {
            traceSource.SubscribeTrace(
                observer);
        }

        public void UnsubscribeTrace(
            ITransportExchangeTraceObserver observer)
        {
            traceSource.UnsubscribeTrace(
                observer);
        }
    }
}
