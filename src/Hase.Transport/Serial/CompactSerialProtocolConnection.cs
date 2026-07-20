namespace Hase.Transport.Serial;

/// <summary>
/// Provides serialized Compact Serial Protocol request/response exchanges over
/// one owned serial byte stream.
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
                stream);
    }

    /// <inheritdoc />
    public event EventHandler<
        TransportConnectionStateChangedEventArgs>?
        StateChanged;

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

            byte[] encodedRequest =
                CompactSerialFrameCodec.Encode(
                    request);

            await _stream.WriteAsync(
                encodedRequest,
                cancellationToken);

            CompactSerialFrame response =
                await _reader.ReadAsync(
                    cancellationToken);

            if (response.CorrelationId
                != request.CorrelationId)
            {
                throw new InvalidDataException(
                    $"Compact serial response correlation identifier "
                    + $"{response.CorrelationId} does not match request "
                    + $"correlation identifier {request.CorrelationId}.");
            }

            return response;
        }
        catch (ObjectDisposedException)
            when (_state == TransportConnectionState.Closed)
        {
            throw;
        }
        catch
        {
            TransitionTo(
                TransportConnectionState.Faulted);

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
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_state == TransportConnectionState.Closed)
        {
            return;
        }

        await _stream.DisposeAsync();

        _exchangeLock.Dispose();

        TransitionTo(
            TransportConnectionState.Closed);
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