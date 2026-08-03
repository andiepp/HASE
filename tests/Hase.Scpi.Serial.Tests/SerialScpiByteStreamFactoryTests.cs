using Hase.Scpi.Serial;
using Hase.Transport.Serial;

namespace Hase.Scpi.Serial.Tests;

public sealed class SerialScpiByteStreamFactoryTests
{
    [Fact]
    public async Task OpenAsync_ForwardsOptionsAndCancellationToken()
    {
        var serial = new StubSerialByteStream();
        var transportFactory = new RecordingSerialByteStreamFactory(serial);
        var factory = new SerialScpiByteStreamFactory(transportFactory);
        var options = new SerialTransportOptions("TEST-PORT", 57600);
        using var cancellation = new CancellationTokenSource();

        await using var stream = await factory.OpenAsync(options, cancellation.Token);

        Assert.Same(options, transportFactory.OpenedOptions);
        Assert.Equal(cancellation.Token, transportFactory.CancellationToken);
    }

    [Fact]
    public async Task OpenAsync_PropagatesTransportFailure()
    {
        var expected = new InvalidOperationException("Open failed.");
        var factory = new SerialScpiByteStreamFactory(
            new ThrowingSerialByteStreamFactory(expected));

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await factory.OpenAsync(new SerialTransportOptions("TEST-PORT", 57600)));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task OpenAsync_RejectsPreCanceledOperationBeforeOpeningTransport()
    {
        var transportFactory = new RecordingSerialByteStreamFactory(new StubSerialByteStream());
        var factory = new SerialScpiByteStreamFactory(transportFactory);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await factory.OpenAsync(
                new SerialTransportOptions("TEST-PORT", 57600),
                cancellation.Token));

        Assert.Null(transportFactory.OpenedOptions);
    }

    [Fact]
    public async Task OpenAsync_RejectsNullOptions()
    {
        var factory = new SerialScpiByteStreamFactory(
            new RecordingSerialByteStreamFactory(new StubSerialByteStream()));

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await factory.OpenAsync(null!));
    }

    [Fact]
    public void Constructor_RejectsNullFactory()
    {
        Assert.Throws<ArgumentNullException>(() => new SerialScpiByteStreamFactory(null!));
    }

    private sealed class RecordingSerialByteStreamFactory(ISerialByteStream stream)
        : ISerialByteStreamFactory
    {
        public SerialTransportOptions? OpenedOptions { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public ValueTask<ISerialByteStream> OpenAsync(
            SerialTransportOptions options,
            CancellationToken cancellationToken = default)
        {
            OpenedOptions = options;
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(stream);
        }
    }

    private sealed class ThrowingSerialByteStreamFactory(Exception exception)
        : ISerialByteStreamFactory
    {
        public ValueTask<ISerialByteStream> OpenAsync(
            SerialTransportOptions options,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<ISerialByteStream>(exception);
    }

    private sealed class StubSerialByteStream : ISerialByteStream
    {
        public ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(0);

        public ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
