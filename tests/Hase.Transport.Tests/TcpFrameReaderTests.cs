using Hase.Transport.Tcp;

namespace Hase.Transport.Tests;

public sealed class TcpFrameReaderTests
{
    [Fact]
    public async Task ReadAsync_ShouldReadEncodedFrame()
    {
        // Arrange
        byte[] expectedPayload =
        [
            0x10,
            0x20,
            0x30
        ];

        byte[] frame =
            TcpFrameCodec.Encode(
                expectedPayload);

        await using var stream =
            new MemoryStream(
                frame);

        // Act
        byte[] actualPayload =
            await TcpFrameReader.ReadAsync(
                stream,
                maximumPayloadLength: 1024);

        // Assert
        Assert.Equal(
            expectedPayload,
            actualPayload);
    }

    [Fact]
    public async Task ReadAsync_FragmentedStream_ShouldReadCompleteFrame()
    {
        // Arrange
        byte[] expectedPayload =
        [
            0x01,
            0x02,
            0x03,
            0x04,
            0x05
        ];

        byte[] frame =
            TcpFrameCodec.Encode(
                expectedPayload);

        await using var stream =
            new FragmentedReadStream(
                frame,
                maximumReadLength: 1);

        // Act
        byte[] actualPayload =
            await TcpFrameReader.ReadAsync(
                stream,
                maximumPayloadLength: 1024);

        // Assert
        Assert.Equal(
            expectedPayload,
            actualPayload);
    }

    [Fact]
    public async Task ReadAsync_EmptyPayload_ShouldReturnEmptyArray()
    {
        // Arrange
        byte[] frame =
            TcpFrameCodec.Encode(
                Array.Empty<byte>());

        await using var stream =
            new MemoryStream(
                frame);

        // Act
        byte[] payload =
            await TcpFrameReader.ReadAsync(
                stream,
                maximumPayloadLength: 1024);

        // Assert
        Assert.Empty(
            payload);
    }

    [Fact]
    public async Task ReadAsync_PayloadExceedsMaximum_ShouldThrow()
    {
        // Arrange
        byte[] frame =
            TcpFrameCodec.Encode(
                new byte[5]);

        await using var stream =
            new MemoryStream(
                frame);

        // Act
        Task Act()
        {
            return TcpFrameReader.ReadAsync(
                stream,
                maximumPayloadLength: 4);
        }

        // Assert
        InvalidDataException exception =
            await Assert.ThrowsAsync<
                InvalidDataException>(
                    Act);

        Assert.Equal(
            "The TCP frame payload length 5 exceeds "
            + "the configured maximum of 4 bytes.",
            exception.Message);
    }

    [Fact]
    public async Task ReadAsync_IncompleteHeader_ShouldThrow()
    {
        // Arrange
        byte[] incompleteHeader =
        [
            0x00,
            0x00
        ];

        await using var stream =
            new MemoryStream(
                incompleteHeader);

        // Act
        Task Act()
        {
            return TcpFrameReader.ReadAsync(
                stream,
                maximumPayloadLength: 1024);
        }

        // Assert
        await Assert.ThrowsAsync<
            EndOfStreamException>(
                Act);
    }

    [Fact]
    public async Task ReadAsync_IncompletePayload_ShouldThrow()
    {
        // Arrange
        byte[] incompleteFrame =
        [
            0x00,
            0x00,
            0x00,
            0x03,
            0x10,
            0x20
        ];

        await using var stream =
            new MemoryStream(
                incompleteFrame);

        // Act
        Task Act()
        {
            return TcpFrameReader.ReadAsync(
                stream,
                maximumPayloadLength: 1024);
        }

        // Assert
        await Assert.ThrowsAsync<
            EndOfStreamException>(
                Act);
    }

    [Fact]
    public async Task ReadAsync_NegativeMaximumPayloadLength_ShouldThrow()
    {
        // Arrange
        await using var stream =
            new MemoryStream();

        // Act
        Task Act()
        {
            return TcpFrameReader.ReadAsync(
                stream,
                maximumPayloadLength: -1);
        }

        // Assert
        ArgumentOutOfRangeException exception =
            await Assert.ThrowsAsync<
                ArgumentOutOfRangeException>(
                    Act);

        Assert.Equal(
            "maximumPayloadLength",
            exception.ParamName);
    }

    private sealed class FragmentedReadStream
        : Stream
    {
        private readonly MemoryStream _innerStream;
        private readonly int _maximumReadLength;

        public FragmentedReadStream(
            byte[] content,
            int maximumReadLength)
        {
            ArgumentNullException.ThrowIfNull(
                content);

            if (maximumReadLength < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumReadLength));
            }

            _innerStream =
                new MemoryStream(
                    content);

            _maximumReadLength =
                maximumReadLength;
        }

        public override bool CanRead =>
            true;

        public override bool CanSeek =>
            false;

        public override bool CanWrite =>
            false;

        public override long Length =>
            throw new NotSupportedException();

        public override long Position
        {
            get =>
                throw new NotSupportedException();

            set =>
                throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(
            byte[] buffer,
            int offset,
            int count)
        {
            return _innerStream.Read(
                buffer,
                offset,
                Math.Min(
                    count,
                    _maximumReadLength));
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            int requestedLength =
                Math.Min(
                    buffer.Length,
                    _maximumReadLength);

            return _innerStream.ReadAsync(
                buffer[..requestedLength],
                cancellationToken);
        }

        public override long Seek(
            long offset,
            SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(
            long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(
            byte[] buffer,
            int offset,
            int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(
            bool disposing)
        {
            if (disposing)
            {
                _innerStream.Dispose();
            }

            base.Dispose(
                disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await _innerStream.DisposeAsync();

            await base.DisposeAsync();
        }
    }
}