using Hase.Transport.Serial;

namespace Hase.Transport.Tests;

public sealed class CompactSerialFrameReaderTests
{
    [Fact]
    public async Task ReadAsync_CompleteFrame_ShouldReturnFrame()
    {
        byte[] encoded =
            CreateEncodedFrame(
                messageType: 0x02,
                correlationId: 0x03,
                payload:
                [
                    0x10,
                    0x20
                ]);

        await using var stream =
            new ChunkedSerialByteStream(
                encoded,
                maximumChunkLength:
                    encoded.Length);

        var reader =
            new CompactSerialFrameReader(
                stream);

        CompactSerialFrame frame =
            await reader.ReadAsync();

        Assert.Equal(
            0x02,
            frame.MessageType);

        Assert.Equal(
            0x03,
            frame.CorrelationId);

        Assert.Equal(
            new byte[]
            {
                0x10,
                0x20
            },
            frame.Payload.ToArray());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task ReadAsync_FragmentedFrame_ShouldReturnFrame(
        int maximumChunkLength)
    {
        byte[] encoded =
            CreateEncodedFrame(
                messageType: 0x04,
                correlationId: 0x05,
                payload:
                [
                    0x30,
                    0x40,
                    0x50
                ]);

        await using var stream =
            new ChunkedSerialByteStream(
                encoded,
                maximumChunkLength);

        var reader =
            new CompactSerialFrameReader(
                stream);

        CompactSerialFrame frame =
            await reader.ReadAsync();

        Assert.Equal(
            0x04,
            frame.MessageType);

        Assert.Equal(
            0x05,
            frame.CorrelationId);
    }

    [Fact]
    public async Task ReadAsync_BootNoiseBeforeFrame_ShouldIgnoreNoise()
    {
        byte[] noise =
        [
            0x00,
            0x41,
            0x72,
            0x64,
            0x75,
            0x69,
            0x6E,
            0x6F,
            0x0D,
            0x0A
        ];

        byte[] encoded =
            CreateEncodedFrame(
                messageType: 0x06,
                correlationId: 0x07,
                payload: []);

        await using var stream =
            new ChunkedSerialByteStream(
                noise.Concat(
                        encoded)
                    .ToArray(),
                maximumChunkLength: 2);

        var reader =
            new CompactSerialFrameReader(
                stream);

        CompactSerialFrame frame =
            await reader.ReadAsync();

        Assert.Equal(
            0x06,
            frame.MessageType);
    }

    [Fact]
    public async Task ReadAsync_OverlappingMarker_ShouldUseSecondFirstByte()
    {
        byte[] prefix =
        [
            CompactSerialFrameConstants.StartMarkerFirstByte
        ];

        byte[] encoded =
            CreateEncodedFrame(
                messageType: 0x08,
                correlationId: 0x09,
                payload: []);

        await using var stream =
            new ChunkedSerialByteStream(
                prefix.Concat(
                        encoded)
                    .ToArray(),
                maximumChunkLength: 1);

        var reader =
            new CompactSerialFrameReader(
                stream);

        CompactSerialFrame frame =
            await reader.ReadAsync();

        Assert.Equal(
            0x08,
            frame.MessageType);
    }

    [Fact]
    public async Task ReadAsync_CorruptedFrameThenValidFrame_ShouldRecover()
    {
        byte[] corrupted =
            CreateEncodedFrame(
                messageType: 0x01,
                correlationId: 0x01,
                payload:
                [
                    0x10
                ]);

        corrupted[^1] ^=
            0xFF;

        byte[] valid =
            CreateEncodedFrame(
                messageType: 0x0A,
                correlationId: 0x0B,
                payload:
                [
                    0x20
                ]);

        await using var stream =
            new ChunkedSerialByteStream(
                corrupted.Concat(
                        valid)
                    .ToArray(),
                maximumChunkLength: 3);

        var reader =
            new CompactSerialFrameReader(
                stream);

        CompactSerialFrame frame =
            await reader.ReadAsync();

        Assert.Equal(
            0x0A,
            frame.MessageType);

        Assert.Equal(
            0x0B,
            frame.CorrelationId);
    }

    [Fact]
    public async Task ReadAsync_UnsupportedVersionThenValidFrame_ShouldRecover()
    {
        byte[] unsupported =
            CreateEncodedFrame(
                messageType: 0x01,
                correlationId: 0x01,
                payload: []);

        unsupported[2] =
            0x02;

        byte[] valid =
            CreateEncodedFrame(
                messageType: 0x0C,
                correlationId: 0x0D,
                payload: []);

        await using var stream =
            new ChunkedSerialByteStream(
                unsupported.Concat(
                        valid)
                    .ToArray(),
                maximumChunkLength: 2);

        var reader =
            new CompactSerialFrameReader(
                stream);

        CompactSerialFrame frame =
            await reader.ReadAsync();

        Assert.Equal(
            0x0C,
            frame.MessageType);
    }

    [Fact]
    public async Task ReadAsync_EndBeforeMarker_ShouldThrowEndOfStream()
    {
        await using var stream =
            new ChunkedSerialByteStream(
                [
                    0x00,
                    0x01
                ],
                maximumChunkLength: 1);

        var reader =
            new CompactSerialFrameReader(
                stream);

        async Task Act()
        {
            _ = await reader.ReadAsync();
        }

        await Assert.ThrowsAsync<EndOfStreamException>(
            Act);
    }

    [Fact]
    public async Task ReadAsync_EndInsideFrame_ShouldThrowEndOfStream()
    {
        byte[] encoded =
            CreateEncodedFrame(
                messageType: 0x01,
                correlationId: 0x01,
                payload:
                [
                    0x10,
                    0x20
                ]);

        await using var stream =
            new ChunkedSerialByteStream(
                encoded[..^1],
                maximumChunkLength: 2);

        var reader =
            new CompactSerialFrameReader(
                stream);

        async Task Act()
        {
            _ = await reader.ReadAsync();
        }

        await Assert.ThrowsAsync<EndOfStreamException>(
            Act);
    }

    [Fact]
    public async Task ReadAsync_CancelledToken_ShouldPropagateCancellation()
    {
        await using var stream =
            new ChunkedSerialByteStream(
                [],
                maximumChunkLength: 1);

        var reader =
            new CompactSerialFrameReader(
                stream);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        async Task Act()
        {
            _ = await reader.ReadAsync(
                cancellationTokenSource.Token);
        }

        await Assert.ThrowsAsync<OperationCanceledException>(
            Act);
    }

    [Fact]
    public void Constructor_NullStream_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactSerialFrameReader(
                null!);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    private static byte[] CreateEncodedFrame(
        byte messageType,
        byte correlationId,
        byte[] payload)
    {
        return CompactSerialFrameCodec.Encode(
            new CompactSerialFrame(
                messageType,
                correlationId,
                payload));
    }

    private sealed class ChunkedSerialByteStream
        : ISerialByteStream
    {
        private readonly byte[] _bytes;
        private readonly int _maximumChunkLength;
        private int _position;

        public ChunkedSerialByteStream(
            byte[] bytes,
            int maximumChunkLength)
        {
            _bytes =
                bytes;

            _maximumChunkLength =
                maximumChunkLength;
        }

        public ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int available =
                _bytes.Length
                - _position;

            if (available == 0)
            {
                return ValueTask.FromResult(
                    0);
            }

            int bytesToRead =
                Math.Min(
                    Math.Min(
                        available,
                        buffer.Length),
                    _maximumChunkLength);

            _bytes.AsMemory(
                    _position,
                    bytesToRead)
                .CopyTo(
                    buffer);

            _position +=
                bytesToRead;

            return ValueTask.FromResult(
                bytesToRead);
        }

        public ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}