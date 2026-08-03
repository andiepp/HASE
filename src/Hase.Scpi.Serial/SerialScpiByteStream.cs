using Hase.Transport.Serial;

namespace Hase.Scpi.Serial;

/// <summary>
/// Adapts an opened serial byte stream to the transport-neutral SCPI byte-stream contract.
/// </summary>
public sealed class SerialScpiByteStream : IScpiByteStream
{
    private readonly ISerialByteStream serialByteStream;
    private readonly TimeProvider timeProvider;
    private readonly object timingLock = new();
    private long? writeStartedTimestamp;
    private TimeSpan? timeToFirstByte;

    public SerialScpiByteStream(ISerialByteStream serialByteStream)
        : this(serialByteStream, TimeProvider.System)
    {
    }

    public SerialScpiByteStream(
        ISerialByteStream serialByteStream,
        TimeProvider timeProvider)
    {
        this.serialByteStream = serialByteStream
            ?? throw new ArgumentNullException(nameof(serialByteStream));
        this.timeProvider = timeProvider
            ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>
    /// Gets the elapsed time from the first write attempt to the first nonempty read.
    /// </summary>
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
        CancellationToken cancellationToken = default)
    {
        lock (timingLock)
        {
            writeStartedTimestamp ??= timeProvider.GetTimestamp();
        }

        return serialByteStream.WriteAsync(bytes, cancellationToken);
    }

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
}
