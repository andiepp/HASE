using Hase.Protocol;
using Hase.Transport;

namespace Hase.Runtime.Transport;

/// <summary>
/// Provides protocol request/response demultiplexing, notification delivery,
/// and logical exchange tracing over one duplex transport connection.
/// </summary>
public sealed class ProtocolDuplexSession
    : ITransportExchangeTraceSource
{
    private const int CreatedState = 0;
    private const int RunningState = 1;
    private const int StoppedState = 2;

    private readonly ITransportDuplexConnection _connection;
    private readonly TimeProvider _timeProvider;

    private readonly BinaryProtocolPayloadCodec _payloadCodec =
        new();

    private readonly ProtocolEnvelopeByteCodec _envelopeByteCodec =
        new();

    private readonly object _syncRoot =
        new();

    private readonly Dictionary<uint, PendingResponse> _pendingResponses =
        [];

    private readonly HashSet<uint> _abandonedCorrelationIds =
        [];

    private readonly List<IProtocolNotificationObserver>
        _notificationObservers =
        [];

    private readonly List<ITransportExchangeTraceObserver> _traceObservers =
        [];

    private int _state =
        CreatedState;

    private long _exchangeSequenceNumber;

    /// <summary>
    /// Initializes a protocol duplex session for one transport connection.
    /// </summary>
    public ProtocolDuplexSession(
        ITransportDuplexConnection connection)
        : this(
            connection,
            TimeProvider.System)
    {
    }

    internal ProtocolDuplexSession(
        ITransportDuplexConnection connection,
        TimeProvider timeProvider)
    {
        _connection =
            connection
            ?? throw new ArgumentNullException(
                nameof(connection));

        _timeProvider =
            timeProvider
            ?? throw new ArgumentNullException(
                nameof(timeProvider));
    }

    /// <summary>
    /// Gets whether the receive pump is currently running.
    /// </summary>
    public bool IsRunning =>
        Volatile.Read(
            ref _state)
        == RunningState;

    /// <summary>
    /// Subscribes a protocol-notification observer.
    /// </summary>
    public void SubscribeNotification(
        IProtocolNotificationObserver observer)
    {
        ArgumentNullException.ThrowIfNull(
            observer);

        lock (_syncRoot)
        {
            if (!_notificationObservers.Any(
                    current =>
                        ReferenceEquals(
                            current,
                            observer)))
            {
                _notificationObservers.Add(
                    observer);
            }
        }
    }

    /// <summary>
    /// Unsubscribes a protocol-notification observer.
    /// </summary>
    public void UnsubscribeNotification(
        IProtocolNotificationObserver observer)
    {
        ArgumentNullException.ThrowIfNull(
            observer);

        lock (_syncRoot)
        {
            _notificationObservers.RemoveAll(
                current =>
                    ReferenceEquals(
                        current,
                        observer));
        }
    }

    /// <inheritdoc />
    public void SubscribeTrace(
        ITransportExchangeTraceObserver observer)
    {
        ArgumentNullException.ThrowIfNull(
            observer);

        lock (_syncRoot)
        {
            if (!_traceObservers.Any(
                    current =>
                        ReferenceEquals(
                            current,
                            observer)))
            {
                _traceObservers.Add(
                    observer);
            }
        }
    }

    /// <inheritdoc />
    public void UnsubscribeTrace(
        ITransportExchangeTraceObserver observer)
    {
        ArgumentNullException.ThrowIfNull(
            observer);

        lock (_syncRoot)
        {
            _traceObservers.RemoveAll(
                current =>
                    ReferenceEquals(
                        current,
                        observer));
        }
    }

    /// <summary>
    /// Runs the single receive pump for this session.
    /// </summary>
    public async Task RunAsync(
        CancellationToken cancellationToken = default)
    {
        int previousState =
            Interlocked.CompareExchange(
                ref _state,
                RunningState,
                CreatedState);

        if (previousState != CreatedState)
        {
            throw new InvalidOperationException(
                "The protocol duplex session receive pump "
                + "can be started only once.");
        }

        Exception? terminalException =
            null;

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                byte[] frame =
                    await _connection.ReceiveAsync(
                        cancellationToken);

                ProcessReceivedFrame(
                    frame);
            }
        }
        catch (Exception exception)
        {
            terminalException =
                exception;

            throw;
        }
        finally
        {
            Interlocked.Exchange(
                ref _state,
                StoppedState);

            CompletePendingResponses(
                terminalException,
                cancellationToken);
        }
    }

    /// <summary>
    /// Sends one protocol request and waits for its correlated response.
    /// </summary>
    public async Task<ProtocolMessage> SendAsync(
        ProtocolMessage request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        cancellationToken.ThrowIfCancellationRequested();

        if (request.Role != ProtocolMessageRole.Request)
        {
            throw new ArgumentException(
                "Only request-role protocol messages can be sent "
                + "through a protocol duplex session.",
                nameof(request));
        }

        if (request.CorrelationId.IsNone)
        {
            throw new ArgumentException(
                "A duplex protocol request must have a nonzero "
                + "correlation identifier.",
                nameof(request));
        }

        EnsureRunning();

        ProtocolEnvelope requestEnvelope =
            _payloadCodec.Encode(
                request);

        byte[] requestFrame =
            _envelopeByteCodec.Encode(
                requestEnvelope);

        uint correlationId =
            request.CorrelationId.Value;

        var completionSource =
            new TaskCompletionSource<ProtocolMessage>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var pendingResponse =
            new PendingResponse(
                completionSource,
                Interlocked.Increment(
                    ref _exchangeSequenceNumber),
                _timeProvider.GetUtcNow(),
                _timeProvider.GetTimestamp(),
                requestFrame.Length);

        lock (_syncRoot)
        {
            EnsureRunning();

            if (_pendingResponses.ContainsKey(
                    correlationId)
                || _abandonedCorrelationIds.Contains(
                    correlationId))
            {
                throw new InvalidOperationException(
                    $"Correlation identifier '{correlationId}' "
                    + "is already pending in this protocol session.");
            }

            _pendingResponses.Add(
                correlationId,
                pendingResponse);
        }

        try
        {
            await _connection.SendAsync(
                requestFrame,
                cancellationToken);
        }
        catch (OperationCanceledException exception)
            when (cancellationToken.IsCancellationRequested)
        {
            if (RemovePendingResponse(
                    correlationId,
                    pendingResponse))
            {
                PublishUnsuccessfulTrace(
                    pendingResponse,
                    TransportExchangeOutcome.Cancelled,
                    exception);
            }

            throw;
        }
        catch (Exception exception)
        {
            if (RemovePendingResponse(
                    correlationId,
                    pendingResponse))
            {
                PublishUnsuccessfulTrace(
                    pendingResponse,
                    TransportExchangeOutcome.Failed,
                    exception);
            }

            throw;
        }

        try
        {
            return await completionSource.Task.WaitAsync(
                cancellationToken);
        }
        catch (OperationCanceledException exception)
            when (cancellationToken.IsCancellationRequested)
        {
            bool removed =
                RemovePendingResponse(
                    correlationId,
                    pendingResponse);

            if (removed)
            {
                lock (_syncRoot)
                {
                    _abandonedCorrelationIds.Add(
                        correlationId);
                }

                PublishUnsuccessfulTrace(
                    pendingResponse,
                    TransportExchangeOutcome.Cancelled,
                    exception);
            }

            throw;
        }
    }

    private void ProcessReceivedFrame(
        byte[] frame)
    {
        ArgumentNullException.ThrowIfNull(
            frame);

        ProtocolEnvelope envelope =
            _envelopeByteCodec.Decode(
                frame);

        ProtocolMessage message =
            _payloadCodec.Decode(
                envelope);

        switch (message.Role)
        {
            case ProtocolMessageRole.Response:
                RouteResponse(
                    message,
                    frame.Length);
                return;

            case ProtocolMessageRole.Notification:
                RouteNotification(
                    message);
                return;

            default:
                throw new InvalidDataException(
                    $"A protocol duplex session cannot receive "
                    + $"a message with role '{message.Role}'.");
        }
    }

    private void RouteResponse(
        ProtocolMessage response,
        int responseByteCount)
    {
        if (response.CorrelationId.IsNone)
        {
            throw new InvalidDataException(
                "A protocol response received through a duplex "
                + "session must have a nonzero correlation identifier.");
        }

        uint correlationId =
            response.CorrelationId.Value;

        PendingResponse? pendingResponse =
            null;

        bool abandonedResponse;

        lock (_syncRoot)
        {
            if (_pendingResponses.Remove(
                    correlationId,
                    out pendingResponse))
            {
                abandonedResponse =
                    false;
            }
            else
            {
                abandonedResponse =
                    _abandonedCorrelationIds.Remove(
                        correlationId);
            }
        }

        if (pendingResponse is not null)
        {
            PublishSuccessfulTrace(
                pendingResponse,
                responseByteCount);

            pendingResponse.CompletionSource.TrySetResult(
                response);

            return;
        }

        if (abandonedResponse)
        {
            return;
        }

        throw new InvalidDataException(
            $"Received a protocol response for unknown correlation "
            + $"identifier '{correlationId}'.");
    }

    private void RouteNotification(
        ProtocolMessage notification)
    {
        if (!notification.CorrelationId.IsNone)
        {
            throw new InvalidDataException(
                "A protocol notification received through a duplex "
                + "session must have correlation identifier zero.");
        }

        IProtocolNotificationObserver[] observers;

        lock (_syncRoot)
        {
            observers =
                _notificationObservers.ToArray();
        }

        foreach (IProtocolNotificationObserver observer
                 in observers)
        {
            try
            {
                observer.OnProtocolNotification(
                    notification);
            }
            catch
            {
                // Notification observers are observational.
            }
        }
    }

    private void CompletePendingResponses(
        Exception? terminalException,
        CancellationToken cancellationToken)
    {
        PendingResponse[] pendingResponses;

        lock (_syncRoot)
        {
            pendingResponses =
                _pendingResponses.Values.ToArray();

            _pendingResponses.Clear();
            _abandonedCorrelationIds.Clear();
        }

        Exception completionException =
            terminalException
            ?? new InvalidOperationException(
                "The protocol duplex session stopped before "
                + "the response was received.");

        bool cancelled =
            terminalException
                is OperationCanceledException
            && cancellationToken.IsCancellationRequested;

        foreach (PendingResponse pendingResponse
                 in pendingResponses)
        {
            PublishUnsuccessfulTrace(
                pendingResponse,
                cancelled
                    ? TransportExchangeOutcome.Cancelled
                    : TransportExchangeOutcome.Failed,
                completionException);

            if (cancelled)
            {
                pendingResponse.CompletionSource.TrySetCanceled(
                    cancellationToken);
            }
            else
            {
                pendingResponse.CompletionSource.TrySetException(
                    completionException);
            }
        }
    }

    private void PublishSuccessfulTrace(
        PendingResponse pendingResponse,
        int responseByteCount)
    {
        PublishTrace(
            new TransportExchangeTrace(
                pendingResponse.SequenceNumber,
                pendingResponse.StartedAtUtc,
                _timeProvider.GetUtcNow(),
                GetElapsedTime(
                    pendingResponse),
                pendingResponse.RequestByteCount,
                responseByteCount,
                TransportExchangeOutcome.Succeeded,
                _connection.State));
    }

    private void PublishUnsuccessfulTrace(
        PendingResponse pendingResponse,
        TransportExchangeOutcome outcome,
        Exception exception)
    {
        PublishTrace(
            new TransportExchangeTrace(
                pendingResponse.SequenceNumber,
                pendingResponse.StartedAtUtc,
                _timeProvider.GetUtcNow(),
                GetElapsedTime(
                    pendingResponse),
                pendingResponse.RequestByteCount,
                responseByteCount:
                    0,
                outcome,
                _connection.State,
                exceptionType:
                    exception.GetType().FullName
                    ?? exception.GetType().Name,
                exceptionMessage:
                    exception.Message));
    }

    private TimeSpan GetElapsedTime(
        PendingResponse pendingResponse)
    {
        return _timeProvider.GetElapsedTime(
            pendingResponse.StartedTimestamp,
            _timeProvider.GetTimestamp());
    }

    private void PublishTrace(
        TransportExchangeTrace trace)
    {
        ITransportExchangeTraceObserver[] observers;

        lock (_syncRoot)
        {
            observers =
                _traceObservers.ToArray();
        }

        foreach (ITransportExchangeTraceObserver observer
                 in observers)
        {
            try
            {
                observer.OnTransportExchangeCompleted(
                    trace);
            }
            catch
            {
                // Trace observers are observational.
            }
        }
    }

    private void EnsureRunning()
    {
        if (!IsRunning)
        {
            throw new InvalidOperationException(
                "The protocol duplex session receive pump "
                + "is not running.");
        }
    }

    private bool RemovePendingResponse(
        uint correlationId,
        PendingResponse pendingResponse)
    {
        lock (_syncRoot)
        {
            if (!_pendingResponses.TryGetValue(
                    correlationId,
                    out PendingResponse? current)
                || !ReferenceEquals(
                    current,
                    pendingResponse))
            {
                return false;
            }

            return _pendingResponses.Remove(
                correlationId);
        }
    }

    private sealed class PendingResponse
    {
        public PendingResponse(
            TaskCompletionSource<ProtocolMessage> completionSource,
            long sequenceNumber,
            DateTimeOffset startedAtUtc,
            long startedTimestamp,
            int requestByteCount)
        {
            CompletionSource =
                completionSource;

            SequenceNumber =
                sequenceNumber;

            StartedAtUtc =
                startedAtUtc;

            StartedTimestamp =
                startedTimestamp;

            RequestByteCount =
                requestByteCount;
        }

        public TaskCompletionSource<ProtocolMessage> CompletionSource
        {
            get;
        }

        public long SequenceNumber
        {
            get;
        }

        public DateTimeOffset StartedAtUtc
        {
            get;
        }

        public long StartedTimestamp
        {
            get;
        }

        public int RequestByteCount
        {
            get;
        }
    }
}