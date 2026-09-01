using Hase.Scpi;
using Hase.Scpi.Serial;
using Hase.Transport.Serial;

namespace Hase.ProtocolExplorer.ScpiCharacterization;

internal sealed class Kel103SerialScpiByteStream : IScpiByteStream
{
    private readonly SerialScpiByteStream serialScpiByteStream;

    public Kel103SerialScpiByteStream(ISerialByteStream serialByteStream)
        : this(new SerialScpiByteStream(serialByteStream), initializeFromGenericStream: true)
    {
    }

    internal Kel103SerialScpiByteStream(
        ISerialByteStream serialByteStream,
        TimeProvider timeProvider)
        : this(
            new SerialScpiByteStream(serialByteStream, timeProvider),
            initializeFromGenericStream: true)
    {
    }

    private Kel103SerialScpiByteStream(
        SerialScpiByteStream serialScpiByteStream,
        bool initializeFromGenericStream)
    {
        _ = initializeFromGenericStream;
        this.serialScpiByteStream = serialScpiByteStream
            ?? throw new ArgumentNullException(nameof(serialScpiByteStream));
    }

    internal static Kel103SerialScpiByteStream FromGenericStream(
        SerialScpiByteStream serialScpiByteStream) =>
        new(serialScpiByteStream, initializeFromGenericStream: true);

    public TimeSpan? TimeToFirstByte => serialScpiByteStream.TimeToFirstByte;

    public ValueTask WriteAsync(
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken = default) =>
        serialScpiByteStream.WriteAsync(bytes, cancellationToken);

    public ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default) =>
        serialScpiByteStream.ReadAsync(buffer, cancellationToken);

    public ValueTask DisposeAsync() => serialScpiByteStream.DisposeAsync();
}
