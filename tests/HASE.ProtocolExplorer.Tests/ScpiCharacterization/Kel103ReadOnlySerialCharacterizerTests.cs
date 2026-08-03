using Xunit;
using System.Text;
using Hase.ProtocolExplorer.ScpiCharacterization;
using Hase.Transport.Serial;

namespace Hase.ProtocolExplorer.Tests.ScpiCharacterization;

public sealed class Kel103ReadOnlySerialCharacterizerTests
{
    [Theory]
    [InlineData(
        "\r",
        1)]
    [InlineData(
        "\n",
        2)]
    [InlineData(
        "\r\n",
        3)]
    [InlineData(
        "",
        0)]
    public async Task CharacterizeAsync_RecognizesKEL103AndResponseTerminator(
        string responseTerminator,
        int expectedResponseTerminatorValue)
    {
        var expectedResponseTerminator =
            (Kel103ResponseTerminator)expectedResponseTerminatorValue;

        const string serialMarker =
            "TEST-SERIAL-DO-NOT-PRINT";

        var stream =
            new FakeSerialByteStream(
                [
                    Encoding.ASCII.GetBytes(
                        "RND 320-KEL103 "),
                    Encoding.ASCII.GetBytes(
                        $"V3.30 SN:{serialMarker}{responseTerminator}")
                ]);

        var factory =
            new FakeSerialByteStreamFactory(
                stream);

        var characterizer =
            new Kel103ReadOnlySerialCharacterizer(
                factory);

        Kel103CharacterizationResult result =
            await characterizer.CharacterizeAsync(
                CreateTransportOptions(),
                CreateFastOptions());

        Assert.True(
            result.IdentityVerified);

        Assert.Equal(
            "KEL-103",
            result.ProductIdentity);

        Assert.Equal(
            "V3.30",
            result.Firmware);

        Assert.Equal(
            expectedResponseTerminator,
            result.ResponseTerminator);

        Assert.False(
            result.CommandEchoDetected);

        Assert.Equal(
            "*IDN?\r",
            Encoding.ASCII.GetString(
                Assert.Single(
                    stream.Writes)));

        Assert.DoesNotContain(
            serialMarker,
            result.SanitizedIdentity,
            StringComparison.Ordinal);

        Assert.Contains(
            "<redacted>",
            result.SanitizedIdentity,
            StringComparison.Ordinal);

        Assert.True(
            stream.Disposed);
    }

    [Fact]
    public async Task CharacterizeAsync_DetectsCommandEcho()
    {
        var stream =
            new FakeSerialByteStream(
                [
                    Encoding.ASCII.GetBytes(
                        "*IDN?\r\nRND 320-KEL103 V3.30 SN:TEST\r\n")
                ]);

        var characterizer =
            new Kel103ReadOnlySerialCharacterizer(
                new FakeSerialByteStreamFactory(
                    stream));

        Kel103CharacterizationResult result =
            await characterizer.CharacterizeAsync(
                CreateTransportOptions(),
                CreateFastOptions());

        Assert.True(
            result.CommandEchoDetected);
    }

    [Fact]
    public async Task CharacterizeAsync_UsesExpectedSerialOptions()
    {
        var stream =
            CreateSuccessfulStream();

        var factory =
            new FakeSerialByteStreamFactory(
                stream);

        var characterizer =
            new Kel103ReadOnlySerialCharacterizer(
                factory);

        SerialTransportOptions expectedOptions =
            CreateTransportOptions();

        await characterizer.CharacterizeAsync(
            expectedOptions,
            CreateFastOptions());

        Assert.Same(
            expectedOptions,
            factory.OpenedOptions);

        Assert.Equal(
            115200,
            factory.OpenedOptions!.BaudRate);

        Assert.Equal(
            8,
            factory.OpenedOptions.DataBits);

        Assert.Equal(
            SerialParity.None,
            factory.OpenedOptions.Parity);

        Assert.Equal(
            SerialStopBits.One,
            factory.OpenedOptions.StopBits);

        Assert.Equal(
            SerialHandshake.None,
            factory.OpenedOptions.Handshake);
    }

    [Fact]
    public async Task CharacterizeAsync_TimesOutWhenReadIgnoresCancellation()
    {
        var stream =
            new FakeSerialByteStream(
                [],
                ignoreReadCancellation: true);

        var characterizer =
            new Kel103ReadOnlySerialCharacterizer(
                new FakeSerialByteStreamFactory(
                    stream));

        await Assert.ThrowsAsync<TimeoutException>(() =>
            characterizer.CharacterizeAsync(
                CreateTransportOptions(),
                new Kel103CharacterizationOptions(
                    Kel103CommandTerminator.CarriageReturn,
                    TimeSpan.FromMilliseconds(80),
                    TimeSpan.FromMilliseconds(20),
                    maximumResponseBytes: 512)));

        Assert.True(
            stream.Disposed);
    }

    [Fact]
    public async Task CharacterizeAsync_RejectsMaximumSizeResponse()
    {
        var stream =
            new FakeSerialByteStream(
                [
                    Encoding.ASCII.GetBytes(
                        "KEL103!!")
                ]);

        var characterizer =
            new Kel103ReadOnlySerialCharacterizer(
                new FakeSerialByteStreamFactory(
                    stream));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            characterizer.CharacterizeAsync(
                CreateTransportOptions(),
                new Kel103CharacterizationOptions(
                    Kel103CommandTerminator.CarriageReturn,
                    TimeSpan.FromMilliseconds(100),
                    TimeSpan.FromMilliseconds(20),
                    maximumResponseBytes: 8)));

        Assert.True(
            stream.Disposed);
    }

    [Fact]
    public async Task CharacterizeAsync_RejectsNonAsciiResponse()
    {
        var stream =
            new FakeSerialByteStream(
                [
                    [
                        0x52,
                        0x4E,
                        0x44,
                        0x00,
                        0x0D
                    ]
                ],
                endAfterChunks: true);

        var characterizer =
            new Kel103ReadOnlySerialCharacterizer(
                new FakeSerialByteStreamFactory(
                    stream));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            characterizer.CharacterizeAsync(
                CreateTransportOptions(),
                CreateFastOptions()));

        Assert.True(
            stream.Disposed);
    }

    [Fact]
    public async Task CharacterizeAsync_RejectsDifferentProduct()
    {
        var stream =
            new FakeSerialByteStream(
                [
                    Encoding.ASCII.GetBytes(
                        "OTHER INSTRUMENT V1.0 SN:TEST\r")
                ],
                endAfterChunks: true);

        var characterizer =
            new Kel103ReadOnlySerialCharacterizer(
                new FakeSerialByteStreamFactory(
                    stream));

        InvalidDataException exception =
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                characterizer.CharacterizeAsync(
                    CreateTransportOptions(),
                    CreateFastOptions()));

        Assert.Contains(
            "does not identify a KEL-103",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task CharacterizeAsync_PropagatesCallerCancellationAndDisposes()
    {
        var stream =
            new FakeSerialByteStream(
                []);

        var characterizer =
            new Kel103ReadOnlySerialCharacterizer(
                new FakeSerialByteStreamFactory(
                    stream));

        using var cancellationSource =
            new CancellationTokenSource();

        cancellationSource.CancelAfter(
            TimeSpan.FromMilliseconds(
                20));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            characterizer.CharacterizeAsync(
                CreateTransportOptions(),
                new Kel103CharacterizationOptions(
                    Kel103CommandTerminator.CarriageReturn,
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromMilliseconds(100),
                    maximumResponseBytes: 512),
                cancellationSource.Token));

        Assert.True(
            stream.Disposed);
    }

    private static SerialTransportOptions CreateTransportOptions()
    {
        return new SerialTransportOptions(
            "TEST-PORT",
            115200);
    }

    private static Kel103CharacterizationOptions CreateFastOptions()
    {
        return new Kel103CharacterizationOptions(
            Kel103CommandTerminator.CarriageReturn,
            TimeSpan.FromMilliseconds(200),
            TimeSpan.FromMilliseconds(20),
            maximumResponseBytes: 512);
    }

    private static FakeSerialByteStream CreateSuccessfulStream()
    {
        return new FakeSerialByteStream(
            [
                Encoding.ASCII.GetBytes(
                    "RND 320-KEL103 V3.30 SN:TEST\r")
            ]);
    }

    private sealed class FakeSerialByteStreamFactory
        : ISerialByteStreamFactory
    {
        private readonly ISerialByteStream _stream;

        public FakeSerialByteStreamFactory(
            ISerialByteStream stream)
        {
            _stream =
                stream;
        }

        public SerialTransportOptions? OpenedOptions
        {
            get;
            private set;
        }

        public ValueTask<ISerialByteStream> OpenAsync(
            SerialTransportOptions options,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            OpenedOptions =
                options;

            return ValueTask.FromResult(
                _stream);
        }
    }

    private sealed class FakeSerialByteStream
        : ISerialByteStream
    {
        private readonly Queue<byte[]> _chunks;
        private readonly bool _endAfterChunks;
        private readonly bool _ignoreReadCancellation;

        public FakeSerialByteStream(
            IEnumerable<byte[]> chunks,
            bool endAfterChunks = false,
            bool ignoreReadCancellation = false)
        {
            _chunks =
                new Queue<byte[]>(
                    chunks);

            _endAfterChunks =
                endAfterChunks;

            _ignoreReadCancellation =
                ignoreReadCancellation;
        }

        public List<byte[]> Writes
        {
            get;
        } = [];

        public bool Disposed
        {
            get;
            private set;
        }

        public async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (_chunks.Count > 0)
            {
                byte[] chunk =
                    _chunks.Dequeue();

                if (chunk.Length > buffer.Length)
                {
                    throw new InvalidOperationException(
                        "The fake response chunk does not fit in the supplied buffer.");
                }

                chunk
                    .AsSpan()
                    .CopyTo(
                        buffer.Span);

                return chunk.Length;
            }

            if (_endAfterChunks)
            {
                return 0;
            }

            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                _ignoreReadCancellation
                    ? CancellationToken.None
                    : cancellationToken);

            return 0;
        }

        public ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Writes.Add(
                buffer.ToArray());

            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Disposed =
                true;

            return ValueTask.CompletedTask;
        }
    }
}
