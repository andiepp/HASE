using System.Net.Sockets;

namespace Hase.Transport.Tcp;

/// <summary>
/// Provides framed request/response exchange over an established
/// TCP connection.
/// </summary>
public sealed class TcpTransportConnection
    : ITransportConnection,
      IAsyncDisposable
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly int _maximumPayloadLength;
    private readonly SemaphoreSlim _exchangeLock =
        new(
            initialCount: 1,
            maxCount: 1);

    private TransportConnectionState _state =
        TransportConnectionState.Connected;

    /// <summary>
    /// Initializes a transport connection over an already-connected
    /// TCP client.
    /// </summary>
    /// <param name="client">
    /// Connected TCP client. Ownership is transferred to this instance.
    /// </param>
    /// <param name="maximumPayloadLength">
    /// Maximum accepted response payload length in bytes.
    /// </param>
    public TcpTransportConnection(
        TcpClient client,
        int maximumPayloadLength)
    {
        ArgumentNullException.ThrowIfNull(
            client);

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
    }

    /// <inheritdoc />
    public event EventHandler<
        TransportConnectionStateChangedEventArgs>?
        StateChanged;

    /// <inheritdoc />
    public TransportConnectionState State =>
        _state;

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

        try
        {
            ThrowIfUnavailable();

            byte[] requestFrame =
                TcpFrameCodec.Encode(
                    request);

            await _stream.WriteAsync(
                requestFrame.AsMemory(),
                cancellationToken);

            await _stream.FlushAsync(
                cancellationToken);

            return await TcpFrameReader.ReadAsync(
                _stream,
                _maximumPayloadLength,
                cancellationToken);
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