using Hase.ProtocolExplorer.ScpiCharacterization;
using Hase.Scpi;
using Hase.Transport.Serial;
using Xunit;

namespace Hase.ProtocolExplorer.Tests.ScpiCharacterization;

public sealed class Kel103SerialScpiByteStreamTests
{
    [Fact]
    public async Task OpenAsync_ForwardsValidatedRuntimeOptions()
    {
        var serialStream = new FakeSerialByteStream();
        var serialFactory = new FakeSerialByteStreamFactory(serialStream);
        var factory = new Kel103SerialScpiByteStreamFactory(serialFactory);
        var options = new SerialTransportOptions("TEST-PORT", 115200);

        await using IScpiByteStream stream = await factory.OpenAsync(options);

        Assert.Same(options, serialFactory.OpenedOptions);
    }

    [Theory]
    [InlineData(9600, 8, SerialParity.None, SerialStopBits.One, SerialHandshake.None)]
    [InlineData(115200, 7, SerialParity.None, SerialStopBits.One, SerialHandshake.None)]
    [InlineData(115200, 8, SerialParity.Odd, SerialStopBits.One, SerialHandshake.None)]
    [InlineData(115200, 8, SerialParity.None, SerialStopBits.Two, SerialHandshake.None)]
    [InlineData(115200, 8, SerialParity.None, SerialStopBits.One, SerialHandshake.RequestToSend)]
    public async Task OpenAsync_RejectsSettingsOutsideCharacterizedProfile(
        int baudRate,
        int dataBits,
        SerialParity parity,
        SerialStopBits stopBits,
        SerialHandshake handshake)
    {
        var serialFactory = new FakeSerialByteStreamFactory(new FakeSerialByteStream());
        var factory = new Kel103SerialScpiByteStreamFactory(serialFactory);
        var options = new SerialTransportOptions(
            "TEST-PORT", baudRate, dataBits, parity, stopBits, handshake);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await factory.OpenAsync(options));

        Assert.Null(serialFactory.OpenedOptions);
    }

    [Fact]
    public async Task Adapter_ForwardsWriteAndRead()
    {
        var serialStream = new FakeSerialByteStream("VALUE\n"u8.ToArray());
        await using var adapter = new Kel103SerialScpiByteStream(serialStream);
        var readBuffer = new byte[16];

        await adapter.WriteAsync("READ?\r"u8.ToArray());
        var read = await adapter.ReadAsync(readBuffer);

        Assert.Equal("READ?\r"u8.ToArray(), Assert.Single(serialStream.Writes));
        Assert.Equal("VALUE\n"u8.ToArray(), readBuffer[..read]);
    }

    [Fact]
    public async Task Adapter_ForwardsCancellationTokens()
    {
        var serialStream = new FakeSerialByteStream();
        await using var adapter = new Kel103SerialScpiByteStream(serialStream);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await adapter.WriteAsync("READ?\r"u8.ToArray(), cancellation.Token));
    }

    [Fact]
    public async Task Adapter_DisposesUnderlyingSerialStream()
    {
        var serialStream = new FakeSerialByteStream();
        var adapter = new Kel103SerialScpiByteStream(serialStream);

        await adapter.DisposeAsync();

        Assert.True(serialStream.Disposed);
    }

    [Fact]
    public void Constructors_RejectNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new Kel103SerialScpiByteStream(null!));
        Assert.Throws<ArgumentNullException>(() => new Kel103SerialScpiByteStreamFactory(null!));
    }

    private sealed class FakeSerialByteStreamFactory(ISerialByteStream stream)
        : ISerialByteStreamFactory
    {
        public SerialTransportOptions? OpenedOptions { get; private set; }

        public ValueTask<ISerialByteStream> OpenAsync(
            SerialTransportOptions options,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenedOptions = options;
            return ValueTask.FromResult(stream);
        }
    }

    private sealed class FakeSerialByteStream(params byte[][] responses) : ISerialByteStream
    {
        private readonly Queue<byte[]> responseQueue = new(responses);

        public List<byte[]> Writes { get; } = [];

        public bool Disposed { get; private set; }

        public ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (responseQueue.Count == 0)
            {
                return ValueTask.FromResult(0);
            }

            var response = responseQueue.Dequeue();
            response.AsSpan().CopyTo(buffer.Span);
            return ValueTask.FromResult(response.Length);
        }

        public ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
