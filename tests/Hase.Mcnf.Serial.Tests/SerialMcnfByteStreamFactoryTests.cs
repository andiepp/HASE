using Hase.Mcnf.Serial;
using Hase.Transport.Serial;

namespace Hase.Mcnf.Serial.Tests;

public sealed class SerialMcnfByteStreamFactoryTests
{
    private static SerialTransportOptions Options() => new(
        "COM12",
        115200,
        dataBits: 8,
        SerialParity.None,
        SerialStopBits.One,
        SerialHandshake.None,
        assertDataTerminalReady: true,
        assertRequestToSend: true);

    [Fact]
    public async Task OpenAsync_ForwardsTheSerialOptions()
    {
        var serialFactory = new RecordingSerialFactory(
            new SerialMcnfByteStreamTests.FakeSerialByteStream());
        var factory = new SerialMcnfByteStreamFactory(serialFactory);

        SerialTransportOptions options = Options();
        await using SerialMcnfByteStream stream =
            await factory.OpenAsync(options, TimeSpan.Zero);

        Assert.Same(options, Assert.Single(serialFactory.OpenedOptions));
    }

    [Fact]
    public async Task OpenAsync_RejectsNegativeSettleDelays()
    {
        var factory = new SerialMcnfByteStreamFactory(
            new RecordingSerialFactory(new SerialMcnfByteStreamTests.FakeSerialByteStream()));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await factory.OpenAsync(Options(), TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public async Task OpenAsync_DisposesTheStreamWhenTheSettleDelayIsCanceled()
    {
        var serialStream = new SerialMcnfByteStreamTests.FakeSerialByteStream();
        var factory = new SerialMcnfByteStreamFactory(
            new RecordingSerialFactory(serialStream));

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await factory.OpenAsync(
                Options(),
                TimeSpan.FromSeconds(30),
                cancellation.Token));

        Assert.True(serialStream.Disposed);
    }

    [Fact]
    public async Task OpenAsync_RejectsCancellationBeforeOpening()
    {
        var serialFactory = new RecordingSerialFactory(
            new SerialMcnfByteStreamTests.FakeSerialByteStream());
        var factory = new SerialMcnfByteStreamFactory(serialFactory);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await factory.OpenAsync(
                Options(),
                TimeSpan.Zero,
                new CancellationToken(canceled: true)));

        Assert.Empty(serialFactory.OpenedOptions);
    }

    [Fact]
    public void Constructor_RequiresTheSerialByteStreamFactory()
    {
        Assert.Throws<ArgumentNullException>(
            () => new SerialMcnfByteStreamFactory(null!));
    }

    private sealed class RecordingSerialFactory : ISerialByteStreamFactory
    {
        private readonly ISerialByteStream stream;

        public RecordingSerialFactory(ISerialByteStream stream)
        {
            this.stream = stream;
        }

        public List<SerialTransportOptions> OpenedOptions { get; } = [];

        public ValueTask<ISerialByteStream> OpenAsync(
            SerialTransportOptions options,
            CancellationToken cancellationToken = default)
        {
            OpenedOptions.Add(options);
            return ValueTask.FromResult(stream);
        }
    }
}
