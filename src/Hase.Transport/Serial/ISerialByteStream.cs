namespace Hase.Transport.Serial;

/// <summary>
/// Provides asynchronous byte-stream access to an opened serial connection.
/// </summary>
/// <remarks>
/// This contract does not preserve message boundaries. Serial transport
/// framing remains the responsibility of the transport connection that owns
/// the byte stream.
/// </remarks>
public interface ISerialByteStream
    : IAsyncDisposable
{
    /// <summary>
    /// Reads available serial bytes into the supplied buffer.
    /// </summary>
    ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes serial bytes from the supplied buffer.
    /// </summary>
    ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default);
}
