using Hase.Transport.Serial;

namespace Hase.Mcnf.Serial;

/// <summary>
/// Adapts an opened serial byte stream to the transport-neutral MCNF
/// byte-stream contract.
/// </summary>
public sealed class SerialMcnfByteStream : IMcnfByteStream
{
    private readonly ISerialByteStream serialByteStream;

    public SerialMcnfByteStream(ISerialByteStream serialByteStream)
    {
        this.serialByteStream = serialByteStream
            ?? throw new ArgumentNullException(nameof(serialByteStream));
    }

    public ValueTask WriteAsync(
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken = default) =>
        serialByteStream.WriteAsync(bytes, cancellationToken);

    public ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default) =>
        serialByteStream.ReadAsync(buffer, cancellationToken);

    public ValueTask DisposeAsync() => serialByteStream.DisposeAsync();
}
