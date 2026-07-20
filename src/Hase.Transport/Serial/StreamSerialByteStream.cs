namespace Hase.Transport.Serial;

/// <summary>
/// Adapts an owned readable and writable <see cref="Stream"/> to the serial
/// byte-stream contract.
/// </summary>
internal sealed class StreamSerialByteStream
    : ISerialByteStream
{
    private readonly Stream _stream;
    private bool _disposed;

    /// <summary>
    /// Initializes a byte stream that takes ownership of the supplied stream.
    /// </summary>
    public StreamSerialByteStream(
        Stream stream)
    {
        ArgumentNullException.ThrowIfNull(
            stream);

        if (!stream.CanRead)
        {
            throw new ArgumentException(
                "The serial byte stream must be readable.",
                nameof(stream));
        }

        if (!stream.CanWrite)
        {
            throw new ArgumentException(
                "The serial byte stream must be writable.",
                nameof(stream));
        }

        _stream =
            stream;
    }

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        cancellationToken.ThrowIfCancellationRequested();

        return _stream.ReadAsync(
            buffer,
            cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        cancellationToken.ThrowIfCancellationRequested();

        return _stream.WriteAsync(
            buffer,
            cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed =
            true;

        await _stream.DisposeAsync();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }
}
