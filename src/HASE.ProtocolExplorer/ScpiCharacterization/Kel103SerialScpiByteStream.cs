using Hase.Scpi;
using Hase.Transport.Serial;

namespace Hase.ProtocolExplorer.ScpiCharacterization;

internal sealed class Kel103SerialScpiByteStream : IScpiByteStream
{
    private readonly ISerialByteStream serialByteStream;
    private readonly TimeProvider timeProvider;
    private readonly object timingLock = new();
    private long? writeStartedTimestamp;
    private TimeSpan? timeToFirstByte;

    public Kel103SerialScpiByteStream(ISerialByteStream serialByteStream)
        : this(serialByteStream, TimeProvider.System)
    {
    }

    internal Kel103SerialScpiByteStream(
        ISerialByteStream serialByteStream,
        TimeProvider timeProvider)
    {
        this.serialByteStream = serialByteStream
            ?? throw new ArgumentNullException(nameof(serialByteStream));
        this.timeProvider = timeProvider
            ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public TimeSpan? TimeToFirstByte
    {
        get
        {
            lock (timingLock)
            {
                return timeToFirstByte;
            }
        }
    }

    public ValueTask WriteAsync(
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken = default) =>
        WriteCoreAsync(bytes, cancellationToken);

    public async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        var bytesRead = await serialByteStream
            .ReadAsync(buffer, cancellationToken)
            .ConfigureAwait(false);

        if (bytesRead > 0)
        {
            lock (timingLock)
            {
                if (timeToFirstByte is null && writeStartedTimestamp is long started)
                {
                    timeToFirstByte = timeProvider.GetElapsedTime(started);
                }
            }
        }

        return bytesRead;
    }

    public ValueTask DisposeAsync() => serialByteStream.DisposeAsync();

    private ValueTask WriteCoreAsync(
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken)
    {
        lock (timingLock)
        {
            writeStartedTimestamp ??= timeProvider.GetTimestamp();
        }

        return serialByteStream.WriteAsync(bytes, cancellationToken);
    }
}
