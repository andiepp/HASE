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

    private bool _disposed;

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
    public async Task<byte[]> ExchangeAsync(
        byte[] request,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);

        ArgumentNullException.ThrowIfNull(
            request);

        cancellationToken.ThrowIfCancellationRequested();

        await _exchangeLock.WaitAsync(
            cancellationToken);

        try
        {
            ObjectDisposedException.ThrowIf(
                _disposed,
                this);

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
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed =
            true;

        _stream.Dispose();
        _client.Dispose();
        _exchangeLock.Dispose();

        return ValueTask.CompletedTask;
    }
}