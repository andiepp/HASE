using Hase.ProtocolExplorer.ScpiCharacterization;
using Hase.Transport.Serial;
using Xunit;

namespace Hase.ProtocolExplorer.Tests.ScpiCharacterization;

public sealed class Kel103SerialScpiTimingTests
{
    [Fact]
    public async Task FirstNonemptyRead_CapturesElapsedTimeFromWriteStart()
    {
        var clock = new ManualTimeProvider();
        var serial = new TimingSerialByteStream(0, 3);
        await using var adapter = new Kel103SerialScpiByteStream(serial, clock);

        await adapter.WriteAsync("A"u8.ToArray());
        clock.Advance(TimeSpan.FromMilliseconds(25));
        Assert.Equal(0, await adapter.ReadAsync(new byte[8]));
        clock.Advance(TimeSpan.FromMilliseconds(15));
        Assert.Equal(3, await adapter.ReadAsync(new byte[8]));

        Assert.Equal(TimeSpan.FromMilliseconds(40), adapter.TimeToFirstByte);
    }

    [Fact]
    public async Task LaterReads_DoNotReplaceFirstByteTiming()
    {
        var clock = new ManualTimeProvider();
        var serial = new TimingSerialByteStream(1, 1);
        await using var adapter = new Kel103SerialScpiByteStream(serial, clock);
        await adapter.WriteAsync("A"u8.ToArray());
        clock.Advance(TimeSpan.FromMilliseconds(10));
        await adapter.ReadAsync(new byte[8]);
        clock.Advance(TimeSpan.FromMilliseconds(90));
        await adapter.ReadAsync(new byte[8]);

        Assert.Equal(TimeSpan.FromMilliseconds(10), adapter.TimeToFirstByte);
    }

    [Fact]
    public async Task EmptyReads_LeaveTimingUnavailable()
    {
        var clock = new ManualTimeProvider();
        await using var adapter = new Kel103SerialScpiByteStream(
            new TimingSerialByteStream(0), clock);
        await adapter.WriteAsync("A"u8.ToArray());
        clock.Advance(TimeSpan.FromSeconds(1));
        await adapter.ReadAsync(new byte[8]);

        Assert.Null(adapter.TimeToFirstByte);
    }

    [Fact]
    public void Constructor_RejectsNullTimeProvider()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new Kel103SerialScpiByteStream(new TimingSerialByteStream(), null!));
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => timestamp;
        public void Advance(TimeSpan value) => timestamp += value.Ticks;
    }

    private sealed class TimingSerialByteStream(params int[] reads) : ISerialByteStream
    {
        private readonly Queue<int> pendingReads = new(reads);
        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(pendingReads.Dequeue());
        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
