using System.Net.Sockets;

namespace Hase.Transport.Tcp;

/// <summary>
/// Provides framed request/response exchange over an established
/// TCP connection.
/// </summary>
public sealed class TcpTransportConnection
    : ITransportConnection,
      ITransportExchangeTraceSource,
      IAsyncDisposable
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly int _maximumPayloadLength;
    private readonly TimeProvider _timeProvider;

    private readonly TransportExchangeTracePublisher _tracePublisher =
        new();

    private readonly SemaphoreSlim _exchangeLock =
        new(
            initialCount: 1,
            maxCount: 1);

    private TransportConnectionState _state =
        TransportConnectionState.Connected;

    private long _exchangeSequenceNumber;

    public TcpTransportConnection(
        TcpClient client,
        int maximumPayloadLength)
        : this(
            client,
            maximumPayloadLength,
            TimeProvider.System)
    {
    }

    internal TcpTransportConnection(
        TcpClient client,
        int maximumPayloadLength,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(
            client);

        ArgumentNullException.ThrowIfNull(
            timeProvider);

        if (maximumPayloadLength < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumPayloadLength),
                maximumPayloadLength,
                "The maximum payload length must not be negative.");
        }

        _client =
            client;

        _stream =
            client.GetStream();

        _maximumPayloadLength =
            maximumPayloadLength;

        _timeProvider =
            timeProvider;
    }

    /// <inheritdoc />
    public event EventHandler<
        TransportConnectionStateChangedEventArgs>?
        StateChanged;

    /// <inheritdoc />
    public TransportConnectionState State =>
        _state;

    /// <inheritdoc />
    public void SubscribeTrace(
        ITransportExchangeTraceObserver observer)
    {
        _tracePublisher.Subscribe(
            observer);
    }

    /// <inheritdoc />
    public void UnsubscribeTrace(
        ITransportExchangeTraceObserver observer)
    {
        _tracePublisher.Unsubscribe(
            observer);
    }

    /// <inheritdoc />
    public async Task<byte[]> ExchangeAsync(
        byte[] request,
        CancellationToken cancellationToken = default)
    {
        ThrowIfUnavailable();

        ArgumentNullException.ThrowIfNull(
            request);

        cancellationToken.ThrowIfCancellationRequested();

        await _exchangeLock.WaitAsync(
            cancellationToken);

        long sequenceNumber =
            0;

        DateTimeOffset startedAtUtc =
            default;

        long startedTimestamp =
            0;

        bool exchangeStarted =
            false;

        try
        {
            ThrowIfUnavailable();

            sequenceNumber =
                ++_exchangeSequenceNumber;

            startedAtUtc =
                _timeProvider.GetUtcNow();

            startedTimestamp =
                _timeProvider.GetTimestamp();

            exchangeStarted =
                true;

            byte[] requestFrame =
                TcpFrameCodec.Encode(
                    request);

            await _stream.WriteAsync(
                requestFrame.AsMemory(),
                cancellationToken);

            await _stream.FlushAsync(
                cancellationToken);

            byte[] response =
                await TcpFrameReader.ReadAsync(
                    _stream,
                    _maximumPayloadLength,
                    cancellationToken);

            PublishTrace(
                sequenceNumber,
                startedAtUtc,
                startedTimestamp,
                request.Length,
                response.Length,
                TransportExchangeOutcome.Succeeded);

            return response;
        }
        catch (ObjectDisposedException)
            when (_state == TransportConnectionState.Closed)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            TransitionTo(
                TransportConnectionState.Faulted);

            if (exchangeStarted)
            {
                PublishTrace(
                    sequenceNumber,
                    startedAtUtc,
                    startedTimestamp,
                    request.Length,
                    responseByteCount:
                        0,
                    TransportExchangeOutcome.Cancelled,
                    exception);
            }

            throw;
        }
        catch (Exception exception)
        {
            TransitionTo(
                TransportConnectionState.Faulted);

            if (exchangeStarted)
            {
                PublishTrace(
                    sequenceNumber,
                    startedAtUtc,
                    startedTimestamp,
                    request.Length,
                    responseByteCount:
                        0,
                    TransportExchangeOutcome.Failed,
                    exception);
            }

            throw;
        }
        finally
        {
            _exchangeLock.Release();
        }
    }

    /// <summary>
    /// Closes the TCP connection and releases all owned resources.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        if (_state == TransportConnectionState.Closed)
        {
            return ValueTask.CompletedTask;
        }

        _stream.Dispose();
        _client.Dispose();
        _exchangeLock.Dispose();

        TransitionTo(
            TransportConnectionState.Closed);

        return ValueTask.CompletedTask;
    }

    private void PublishTrace(
        long sequenceNumber,
        DateTimeOffset startedAtUtc,
        long startedTimestamp,
        int requestByteCount,
        int responseByteCount,
        TransportExchangeOutcome outcome,
        Exception? exception = null)
    {
        DateTimeOffset completedAtUtc =
            _timeProvider.GetUtcNow();

        TimeSpan duration =
            _timeProvider.GetElapsedTime(
                startedTimestamp,
                _timeProvider.GetTimestamp());

        _tracePublisher.Publish(
            new TransportExchangeTrace(
                sequenceNumber,
                startedAtUtc,
                completedAtUtc,
                duration,
                requestByteCount,
                responseByteCount,
                outcome,
                _state,
                exceptionType:
                    exception?.GetType().FullName,
                exceptionMessage:
                    exception?.Message));
    }

    private void ThrowIfUnavailable()
    {
        if (_state == TransportConnectionState.Closed)
        {
            throw new ObjectDisposedException(
                nameof(TcpTransportConnection));
        }

        if (_state == TransportConnectionState.Faulted)
        {
            throw new InvalidOperationException(
                "The TCP transport connection is faulted "
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