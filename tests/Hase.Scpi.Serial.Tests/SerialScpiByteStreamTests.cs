using Hase.Scpi.Serial;
using Hase.Transport.Serial;

namespace Hase.Scpi.Serial.Tests;

public sealed class SerialScpiByteStreamTests
{
    [Fact]
    public async Task ReadAndWrite_ForwardBytesAndCancellationTokens()
    {
        var serial = new RecordingSerialByteStream("VALUE\n"u8.ToArray());
        await using var stream = new SerialScpiByteStream(serial);
        using var cancellation = new CancellationTokenSource();
        var readBuffer = new byte[16];

        await stream.WriteAsync("READ?\r"u8.ToArray(), cancellation.Token);
        var read = await stream.ReadAsync(readBuffer, cancellation.Token);

        Assert.Equal("READ?\r"u8.ToArray(), Assert.Single(serial.Writes));
        Assert.Equal("VALUE\n"u8.ToArray(), readBuffer[..read]);
        Assert.Equal(cancellation.Token, serial.WriteCancellationToken);
        Assert.Equal(cancellation.Token, serial.ReadCancellationToken);
    }

    [Fact]
    public async Task DisposeAsync_DisposesUnderlyingStream()
    {
        var serial = new RecordingSerialByteStream();
        var stream = new SerialScpiByteStream(serial);

        await stream.DisposeAsync();

        Assert.True(serial.Disposed);
    }

    [Fact]
    public async Task FirstNonemptyRead_CapturesTimeFromFirstWriteAttemptOnce()
    {
        var timeProvider = new ManualTimeProvider();
        var serial = new RecordingSerialByteStream([], "A"u8.ToArray(), "B"u8.ToArray());
        await using var stream = new SerialScpiByteStream(serial, timeProvider);

        await stream.WriteAsync("FIRST"u8.ToArray());
        timeProvider.Advance(TimeSpan.FromMilliseconds(10));
        await stream.WriteAsync("SECOND"u8.ToArray());
        timeProvider.Advance(TimeSpan.FromMilliseconds(15));
        Assert.Equal(0, await stream.ReadAsync(new byte[8]));
        timeProvider.Advance(TimeSpan.FromMilliseconds(20));
        Assert.Equal(1, await stream.ReadAsync(new byte[8]));
        timeProvider.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Equal(1, await stream.ReadAsync(new byte[8]));

        Assert.Equal(TimeSpan.FromMilliseconds(45), stream.TimeToFirstByte);
    }

    [Fact]
    public async Task ReadBeforeWrite_DoesNotCaptureTiming()
    {
        var timeProvider = new ManualTimeProvider();
        var serial = new RecordingSerialByteStream("A"u8.ToArray());
        await using var stream = new SerialScpiByteStream(serial, timeProvider);

        Assert.Equal(1, await stream.ReadAsync(new byte[8]));

        Assert.Null(stream.TimeToFirstByte);
    }

    [Fact]
    public void Constructors_RejectNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new SerialScpiByteStream(null!));
        Assert.Throws<ArgumentNullException>(() =>
            new SerialScpiByteStream(new RecordingSerialByteStream(), null!));
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => timestamp;

        public void Advance(TimeSpan value) => timestamp += value.Ticks;
    }

    private sealed class RecordingSerialByteStream(params byte[][] responses) : ISerialByteStream
    {
        private readonly Queue<byte[]> responseQueue = new(responses);

        public List<byte[]> Writes { get; } = [];

        public CancellationToken ReadCancellationToken { get; private set; }

        public CancellationToken WriteCancellationToken { get; private set; }

        public bool Disposed { get; private set; }

        public ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            ReadCancellationToken = cancellationToken;
            var response = responseQueue.Dequeue();
            response.AsSpan().CopyTo(buffer.Span);
            return ValueTask.FromResult(response.Length);
        }

        public ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            WriteCancellationToken = cancellationToken;
            Writes.Add(buffer.ToArray());
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
