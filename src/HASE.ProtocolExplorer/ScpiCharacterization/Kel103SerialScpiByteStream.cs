using Hase.Scpi;
using Hase.Transport.Serial;

namespace Hase.ProtocolExplorer.ScpiCharacterization;

internal sealed class Kel103SerialScpiByteStream : IScpiByteStream
{
    private readonly ISerialByteStream serialByteStream;

    public Kel103SerialScpiByteStream(ISerialByteStream serialByteStream)
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
