using Hase.Transport;
using Hase.Transport.Serial;

namespace Hase.CompactProtocol;

/// <summary>
/// Provides serialized Compact Serial Protocol request/response exchanges and
/// unsolicited-frame delivery over one owned serial byte stream.
/// </summary>
internal sealed class CompactSerialProtocolConnection
    : ICompactSerialProtocolConnection
{
    private readonly ISerialByteStream _stream;
    private readonly CompactSerialFrameReader _reader;

    private readonly SemaphoreSlim _exchangeLock =
        new(
            initialCount:
                1,
            maxCount:
                1);

    private readonly CancellationTokenSource _receiveLoopCancellation =
        new();

    private readonly object _pendingGate =
        new();

    private Task? _receiveLoopTask;
    private TaskCompletionSource<CompactSerialFrame>? _pendingResponse;
    private byte _pendingCorrelationId;

    private TransportConnectionState _state =
        TransportConnectionState.Connected;

    public CompactSerialProtocolConnection(
        ISerialByteStream stream)
    {
        _stream =
            stream
            ?? throw new ArgumentNullException(
                nameof(stream));

        _reader =
            new CompactSerialFrameReader(
                stream,
                rejectUnsupportedProtocolVersion: true);
    }

    /// <inheritdoc />
    public event EventHandler<
        TransportConnectionStateChangedEventArgs>?
        StateChanged;

    /// <summary>
    /// Occurs when the single compact receive loop receives a valid
    /// zero-correlation unsolicited frame.
    /// </summary>
    public event Action<CompactSerialFrame>?
        UnsolicitedFrameReceived;

    /// <inheritdoc />
    public TransportConnectionState State =>
        _state;

    /// <inheritdoc />
    public async Task<CompactSerialFrame> ExchangeAsync(
        CompactSerialFrame request,
        CancellationToken cancellationToken = default)
    {
        ThrowIfUnavailable();

        ArgumentNullException.ThrowIfNull(
            request);

        if (request.CorrelationId == 0)
        {
            throw new ArgumentException(
                "A compact serial request must use a nonzero "
                + "correlation identifier.",
                nameof(request));
        }

        cancellationToken.ThrowIfCancellationRequested();

        await _exchangeLock.WaitAsync(
            cancellationToken);

        try
        {
            ThrowIfUnavailable();

            var pendingResponse =
                new TaskCompletionSource<CompactSerialFrame>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_pendingGate)
            {
                if (_pendingResponse is not null)
                {
                    throw new InvalidOperationException(
                        "A compact serial response is already pending.");
                }

                _pendingCorrelationId =
                    request.CorrelationId;

                _pendingResponse =
                    pendingResponse;
            }

            EnsureReceiveLoopStarted();

            byte[] encodedRequest =
                CompactSerialFrameCodec.Encode(
                    request);

            try
            {
                await _stream.WriteAsync(
                    encodedRequest,
                    cancellationToken);
            }
            catch
            {
                ClearPendingResponse(
                    pendingResponse);

                FaultFromExchange();

                throw;
            }

            try
            {
                return await pendingResponse.Task.WaitAsync(
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                ClearPendingResponse(
                    pendingResponse);

                FaultFromExchange();

                throw;
            }
        }
        catch (ObjectDisposedException)
            when (_state == TransportConnectionState.Closed)
        {
            throw;
        }
        finally
        {
            _exchangeLock.Release();
        }
    }

    /// <inheritdoc />
    public void Invalidate()
    {
        if (_state == TransportConnectionState.Closed)
        {
            throw new ObjectDisposedException(
                nameof(CompactSerialProtocolConnection));
        }

        TransitionTo(
            TransportConnectionState.Faulted);

        _receiveLoopCancellation.Cancel();

        CompletePendingResponseWithException(
            new InvalidOperationException(
                "The compact serial protocol connection was invalidated."));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_state == TransportConnectionState.Closed)
        {
            return;
        }

        _receiveLoopCancellation.Cancel();

        CompletePendingResponseWithException(
            new ObjectDisposedException(
                nameof(CompactSerialProtocolConnection)));

        await _stream.DisposeAsync();

        Task? receiveLoopTask =
            _receiveLoopTask;

        if (receiveLoopTask is not null)
        {
            try
            {
                await receiveLoopTask;
            }
            catch (OperationCanceledException)
                when (_receiveLoopCancellation.IsCancellationRequested)
            {
            }
            catch (ObjectDisposedException)
                when (_receiveLoopCancellation.IsCancellationRequested)
            {
            }
            catch (EndOfStreamException)
                when (_receiveLoopCancellation.IsCancellationRequested)
            {
            }
        }

        _receiveLoopCancellation.Dispose();
        _exchangeLock.Dispose();

        TransitionTo(
            TransportConnectionState.Closed);
    }

    private void EnsureReceiveLoopStarted()
    {
        if (_receiveLoopTask is not null)
        {
            return;
        }

        _receiveLoopTask =
            RunReceiveLoopAsync(
                _receiveLoopCancellation.Token);
    }

    private async Task RunReceiveLoopAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                CompactSerialFrame frame =
                    await _reader.ReadAsync(
                        cancellationToken);

                if (frame.CorrelationId == 0)
                {
                    if (frame.MessageType
                        != (byte)CompactSerialMessageType.EventNotification)
                    {
                        throw new InvalidDataException(
                            $"Compact serial unsolicited message type "
                            + $"0x{frame.MessageType:X2} is not "
                            + $"{CompactSerialMessageType.EventNotification}.");
                    }

                    UnsolicitedFrameReceived?.Invoke(
                        frame);

                    continue;
                }

                if (frame.MessageType
                    == (byte)CompactSerialMessageType.EventNotification)
                {
                    throw new InvalidDataException(
                        "A compact event notification must use correlation "
                        + "identifier zero.");
                }

                TaskCompletionSource<CompactSerialFrame>? pendingResponse;
                byte pendingCorrelationId;

                lock (_pendingGate)
                {
                    pendingResponse =
                        _pendingResponse;

                    pendingCorrelationId =
                        _pendingCorrelationId;
                }

                if (pendingResponse is null)
                {
                    throw new InvalidDataException(
                        $"Compact serial response correlation identifier "
                        + $"{frame.CorrelationId} has no pending request.");
                }

                if (frame.CorrelationId != pendingCorrelationId)
                {
                    throw new InvalidDataException(
                        $"Compact serial response correlation identifier "
                        + $"{frame.CorrelationId} does not match request "
                        + $"correlation identifier {pendingCorrelationId}.");
                }

                ClearPendingResponse(
                    pendingResponse);

                pendingResponse.TrySetResult(
                    frame);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            TransitionTo(
                TransportConnectionState.Faulted);

            CompletePendingResponseWithException(
                exception);
        }
    }

    private void ClearPendingResponse(
        TaskCompletionSource<CompactSerialFrame> pendingResponse)
    {
        lock (_pendingGate)
        {
            if (!ReferenceEquals(
                    _pendingResponse,
                    pendingResponse))
            {
                return;
            }

            _pendingResponse =
                null;

            _pendingCorrelationId =
                0;
        }
    }

    private void CompletePendingResponseWithException(
        Exception exception)
    {
        TaskCompletionSource<CompactSerialFrame>? pendingResponse;

        lock (_pendingGate)
        {
            pendingResponse =
                _pendingResponse;

            _pendingResponse =
                null;

            _pendingCorrelationId =
                0;
        }

        pendingResponse?.TrySetException(
            exception);
    }

    private void FaultFromExchange()
    {
        TransitionTo(
            TransportConnectionState.Faulted);

        _receiveLoopCancellation.Cancel();
    }

    private void ThrowIfUnavailable()
    {
        if (_state == TransportConnectionState.Closed)
        {
            throw new ObjectDisposedException(
                nameof(CompactSerialProtocolConnection));
        }

        if (_state == TransportConnectionState.Faulted)
        {
            throw new InvalidOperationException(
                "The compact serial protocol connection is faulted "
                + "and cannot be reused.");
        }
    }

    private void TransitionTo(
        TransportConnectionState currentState)
    {
        TransportConnectionState previousState =
            _state;

        if (previousState == currentState)
        {
            return;
        }

        _state =
            currentState;

        StateChanged?.Invoke(
            this,
            new TransportConnectionStateChangedEventArgs(
                previousState,
                currentState));
    }
}