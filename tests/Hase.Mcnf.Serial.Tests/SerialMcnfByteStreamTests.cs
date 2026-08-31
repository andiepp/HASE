using Hase.Mcnf.Serial;
using Hase.Transport.Serial;

namespace Hase.Mcnf.Serial.Tests;

public sealed class SerialMcnfByteStreamTests
{
    [Fact]
    public async Task WriteAsync_DelegatesToTheSerialByteStream()
    {
        var serial = new FakeSerialByteStream();
        var stream = new SerialMcnfByteStream(serial);

        await stream.WriteAsync(new byte[] { 0xA1 });

        Assert.Equal(new byte[] { 0xA1 }, Assert.Single(serial.Writes));
    }

    [Fact]
    public async Task ReadAsync_DelegatesToTheSerialByteStream()
    {
        var serial = new FakeSerialByteStream { NextRead = [0x21] };
        var stream = new SerialMcnfByteStream(serial);

        var buffer = new byte[4];
        int count = await stream.ReadAsync(buffer);

        Assert.Equal(1, count);
        Assert.Equal(0x21, buffer[0]);
    }

    [Fact]
    public async Task DisposeAsync_DisposesTheSerialByteStream()
    {
        var serial = new FakeSerialByteStream();
        var stream = new SerialMcnfByteStream(serial);

        await stream.DisposeAsync();

        Assert.True(serial.Disposed);
    }

    [Fact]
    public void Constructor_RequiresTheSerialByteStream()
    {
        Assert.Throws<ArgumentNullException>(() => new SerialMcnfByteStream(null!));
    }

    internal sealed class FakeSerialByteStream : ISerialByteStream
    {
        public List<byte[]> Writes { get; } = [];

        public byte[] NextRead { get; set; } = [];

        public bool Disposed { get; private set; }

        public ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            Writes.Add(buffer.ToArray());
            return ValueTask.CompletedTask;
        }

        public ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            int count = Math.Min(buffer.Length, NextRead.Length);
            NextRead.AsSpan(0, count).CopyTo(buffer.Span);
            return ValueTask.FromResult(count);
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
