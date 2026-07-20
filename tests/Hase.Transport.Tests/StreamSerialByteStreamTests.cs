using Hase.Transport.Serial;

namespace Hase.Transport.Tests;

public sealed class StreamSerialByteStreamTests
{
    [Fact]
    public async Task WriteAsync_ShouldWriteSuppliedBytes()
    {
        var stream =
            new MemoryStream();

        await using var serialStream =
            new StreamSerialByteStream(
                stream);

        byte[] bytes =
        [
            0x01,
            0x02,
            0x03
        ];

        await serialStream.WriteAsync(
            bytes);

        Assert.Equal(
            bytes,
            stream.ToArray());
    }

    [Fact]
    public async Task ReadAsync_ShouldReadIntoSuppliedBuffer()
    {
        byte[] expected =
        [
            0x10,
            0x20,
            0x30
        ];

        var stream =
            new MemoryStream(
                expected);

        await using var serialStream =
            new StreamSerialByteStream(
                stream);

        var buffer =
            new byte[8];

        int bytesRead =
            await serialStream.ReadAsync(
                buffer);

        Assert.Equal(
            expected.Length,
            bytesRead);

        Assert.Equal(
            expected,
            buffer[..bytesRead]);
    }

    [Fact]
    public async Task ReadAsync_CancelledToken_ShouldThrow()
    {
        await using var serialStream =
            new StreamSerialByteStream(
                new MemoryStream());

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        async Task Act()
        {
            _ = await serialStream.ReadAsync(
                new byte[1],
                cancellationTokenSource.Token);
        }

        await Assert.ThrowsAsync<OperationCanceledException>(
            Act);
    }

    [Fact]
    public async Task WriteAsync_CancelledToken_ShouldThrow()
    {
        await using var serialStream =
            new StreamSerialByteStream(
                new MemoryStream());

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        async Task Act()
        {
            await serialStream.WriteAsync(
                new byte[1],
                cancellationTokenSource.Token);
        }

        await Assert.ThrowsAsync<OperationCanceledException>(
            Act);
    }

    [Fact]
    public async Task DisposeAsync_ShouldDisposeOwnedStreamAndBeIdempotent()
    {
        var stream =
            new MemoryStream();

        var serialStream =
            new StreamSerialByteStream(
                stream);

        await serialStream.DisposeAsync();
        await serialStream.DisposeAsync();

        Assert.False(
            stream.CanRead);

        Assert.False(
            stream.CanWrite);
    }

    [Fact]
    public async Task ReadAsync_AfterDisposal_ShouldThrow()
    {
        var serialStream =
            new StreamSerialByteStream(
                new MemoryStream());

        await serialStream.DisposeAsync();

        async Task Act()
        {
            _ = await serialStream.ReadAsync(
                new byte[1]);
        }

        await Assert.ThrowsAsync<ObjectDisposedException>(
            Act);
    }

    [Fact]
    public async Task WriteAsync_AfterDisposal_ShouldThrow()
    {
        var serialStream =
            new StreamSerialByteStream(
                new MemoryStream());

        await serialStream.DisposeAsync();

        async Task Act()
        {
            await serialStream.WriteAsync(
                new byte[1]);
        }

        await Assert.ThrowsAsync<ObjectDisposedException>(
            Act);
    }

    [Fact]
    public void Constructor_NullStream_ShouldThrow()
    {
        void Act()
        {
            _ = new StreamSerialByteStream(
                null!);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NonReadableStream_ShouldThrow()
    {
        using var stream =
            new WriteOnlyStream();

        void Act()
        {
            _ = new StreamSerialByteStream(
                stream);
        }

        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                Act);

        Assert.Equal(
            "stream",
            exception.ParamName);
    }

    [Fact]
    public void Constructor_NonWritableStream_ShouldThrow()
    {
        using var stream =
            new ReadOnlyStream();

        void Act()
        {
            _ = new StreamSerialByteStream(
                stream);
        }

        ArgumentException exception =
            Assert.Throws<ArgumentException>(
                Act);

        Assert.Equal(
            "stream",
            exception.ParamName);
    }

    private sealed class WriteOnlyStream
        : MemoryStream
    {
        public override bool CanRead =>
            false;
    }

    private sealed class ReadOnlyStream
        : MemoryStream
    {
        public override bool CanWrite =>
            false;
    }
}