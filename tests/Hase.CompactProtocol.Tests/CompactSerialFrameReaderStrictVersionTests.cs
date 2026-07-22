using Hase.Transport.Serial;

namespace Hase.CompactProtocol.Tests;

public sealed class CompactSerialFrameReaderStrictVersionTests
{
    [Fact]
    public async Task ReadAsync_StrictReaderUnsupportedVersion_ShouldThrow()
    {
        // Arrange
        byte[] encoded =
            CompactSerialFrameCodec.Encode(
                new CompactSerialFrame(
                    messageType: 0x02,
                    correlationId: 0x03,
                    payload: []));

        encoded[2] =
            0x02;

        await using var stream =
            new MemorySerialByteStream(
                encoded);

        var reader =
            new CompactSerialFrameReader(
                stream,
                rejectUnsupportedProtocolVersion: true);

        // Act
        async Task Act()
        {
            _ = await reader.ReadAsync();
        }

        // Assert
        CompactProtocolVersionNotSupportedException exception =
            await Assert.ThrowsAsync<
                CompactProtocolVersionNotSupportedException>(
                    Act);

        Assert.Equal(
            (byte)2,
            exception.ActualVersion);

        Assert.Equal(
            CompactSerialFrameConstants.ProtocolVersion,
            exception.SupportedVersion);
    }

    [Fact]
    public async Task ReadAsync_DefaultReaderUnsupportedVersion_ShouldRecover()
    {
        // Arrange
        byte[] unsupported =
            CompactSerialFrameCodec.Encode(
                new CompactSerialFrame(
                    messageType: 0x01,
                    correlationId: 0x01,
                    payload: []));

        unsupported[2] =
            0x02;

        byte[] valid =
            CompactSerialFrameCodec.Encode(
                new CompactSerialFrame(
                    messageType: 0x04,
                    correlationId: 0x05,
                    payload: []));

        await using var stream =
            new MemorySerialByteStream(
                unsupported
                    .Concat(
                        valid)
                    .ToArray());

        var reader =
            new CompactSerialFrameReader(
                stream);

        // Act
        CompactSerialFrame frame =
            await reader.ReadAsync();

        // Assert
        Assert.Equal(
            0x04,
            frame.MessageType);

        Assert.Equal(
            0x05,
            frame.CorrelationId);
    }

    private sealed class MemorySerialByteStream
        : ISerialByteStream
    {
        private readonly byte[] _bytes;
        private int _position;

        public MemorySerialByteStream(
            byte[] bytes)
        {
            _bytes =
                bytes;
        }

        public ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            int available =
                _bytes.Length
                - _position;

            if (available == 0)
            {
                return ValueTask.FromResult(
                    0);
            }

            int count =
                Math.Min(
                    available,
                    buffer.Length);

            _bytes
                .AsMemory(
                    _position,
                    count)
                .CopyTo(
                    buffer);

            _position +=
                count;

            return ValueTask.FromResult(
                count);
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