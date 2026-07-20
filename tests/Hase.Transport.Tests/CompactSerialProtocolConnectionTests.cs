using Hase.Transport.Serial;

namespace Hase.Transport.Tests;

public sealed class CompactSerialProtocolConnectionTests
{
    [Fact]
    public async Task ExchangeAsync_ValidResponse_ShouldWriteRequestAndReturnResponse()
    {
        var response =
            new CompactSerialFrame(
                messageType: 0x02,
                correlationId: 0x21,
                payload:
                [
                    0x30,
                    0x40
                ]);

        var stream =
            new TestSerialByteStream(
                CompactSerialFrameCodec.Encode(
                    response));

        await using var connection =
            new CompactSerialProtocolConnection(
                stream);

        var request =
            new CompactSerialFrame(
                messageType: 0x01,
                correlationId: 0x21,
                payload:
                [
                    0x10,
                    0x20
                ]);

        CompactSerialFrame actualResponse =
            await connection.ExchangeAsync(
                request);

        Assert.Equal(
            CompactSerialFrameCodec.Encode(
                request),
            Assert.Single(
                stream.Writes));

        Assert.Equal(
            response.MessageType,
            actualResponse.MessageType);

        Assert.Equal(
            response.CorrelationId,
            actualResponse.CorrelationId);

        Assert.Equal(
            response.Payload.ToArray(),
            actualResponse.Payload.ToArray());

        Assert.Equal(
            TransportConnectionState.Connected,
            connection.State);
    }

    [Fact]
    public async Task ExchangeAsync_MismatchedCorrelation_ShouldFaultConnection()
    {
        var response =
            new CompactSerialFrame(
                messageType: 0x02,
                correlationId: 0x22,
                payload: []);

        var stream =
            new TestSerialByteStream(
                CompactSerialFrameCodec.Encode(
                    response));

        await using var connection =
            new CompactSerialProtocolConnection(
                stream);

        async Task Act()
        {
            _ = await connection.ExchangeAsync(
                new CompactSerialFrame(
                    messageType: 0x01,
                    correlationId: 0x21,
                    payload: []));
        }

        await Assert.ThrowsAsync<InvalidDataException>(
            Act);

        Assert.Equal(
            TransportConnectionState.Faulted,
            connection.State);
    }

    [Fact]
    public async Task ExchangeAsync_ZeroCorrelationRequest_ShouldRejectWithoutWriting()
    {
        var stream =
            new TestSerialByteStream();

        await using var connection =
            new CompactSerialProtocolConnection(
                stream);

        async Task Act()
        {
            _ = await connection.ExchangeAsync(
                new CompactSerialFrame(
                    messageType: 0x01,
                    correlationId: 0x00,
                    payload: []));
        }

        await Assert.ThrowsAsync<ArgumentException>(
            Act);

        Assert.Empty(
            stream.Writes);

        Assert.Equal(
            TransportConnectionState.Connected,
            connection.State);
    }

    [Fact]
    public async Task ExchangeAsync_WriteFailure_ShouldFaultConnection()
    {
        var stream =
            new TestSerialByteStream
            {
                WriteException =
                    new IOException(
                        "Serial write failed.")
            };

        await using var connection =
            new CompactSerialProtocolConnection(
                stream);

        async Task Act()
        {
            _ = await connection.ExchangeAsync(
                CreateRequest());
        }

        await Assert.ThrowsAsync<IOException>(
            Act);

        Assert.Equal(
            TransportConnectionState.Faulted,
            connection.State);
    }

    [Fact]
    public async Task ExchangeAsync_EndOfStream_ShouldFaultConnection()
    {
        var stream =
            new TestSerialByteStream();

        await using var connection =
            new CompactSerialProtocolConnection(
                stream);

        async Task Act()
        {
            _ = await connection.ExchangeAsync(
                CreateRequest());
        }

        await Assert.ThrowsAsync<EndOfStreamException>(
            Act);

        Assert.Equal(
            TransportConnectionState.Faulted,
            connection.State);
    }

    [Fact]
    public async Task ExchangeAsync_Cancellation_ShouldFaultConnection()
    {
        var stream =
            new TestSerialByteStream();

        await using var connection =
            new CompactSerialProtocolConnection(
                stream);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        async Task Act()
        {
            _ = await connection.ExchangeAsync(
                CreateRequest(),
                cancellationTokenSource.Token);
        }

        await Assert.ThrowsAsync<OperationCanceledException>(
            Act);

        Assert.Equal(
            TransportConnectionState.Connected,
            connection.State);
    }

    [Fact]
    public async Task Invalidate_ConnectedConnection_ShouldTransitionToFaultedOnce()
    {
        var stream =
            new TestSerialByteStream();

        await using var connection =
            new CompactSerialProtocolConnection(
                stream);

        var changes =
            new List<TransportConnectionStateChangedEventArgs>();

        connection.StateChanged +=
            (
                sender,
                change) =>
            {
                changes.Add(
                    change);
            };

        connection.Invalidate();
        connection.Invalidate();

        Assert.Equal(
            TransportConnectionState.Faulted,
            connection.State);

        TransportConnectionStateChangedEventArgs change =
            Assert.Single(
                changes);

        Assert.Equal(
            TransportConnectionState.Connected,
            change.PreviousState);

        Assert.Equal(
            TransportConnectionState.Faulted,
            change.CurrentState);
    }

    [Fact]
    public async Task DisposeAsync_ShouldDisposeStreamAndTransitionToClosedOnce()
    {
        var stream =
            new TestSerialByteStream();

        var connection =
            new CompactSerialProtocolConnection(
                stream);

        await connection.DisposeAsync();
        await connection.DisposeAsync();

        Assert.Equal(
            1,
            stream.DisposeCallCount);

        Assert.Equal(
            TransportConnectionState.Closed,
            connection.State);
    }

    [Fact]
    public async Task ExchangeAsync_FaultedConnection_ShouldThrowWithoutWriting()
    {
        var stream =
            new TestSerialByteStream();

        await using var connection =
            new CompactSerialProtocolConnection(
                stream);

        connection.Invalidate();

        async Task Act()
        {
            _ = await connection.ExchangeAsync(
                CreateRequest());
        }

        await Assert.ThrowsAsync<InvalidOperationException>(
            Act);

        Assert.Empty(
            stream.Writes);
    }

    [Fact]
    public async Task ExchangeAsync_ClosedConnection_ShouldThrow()
    {
        var connection =
            new CompactSerialProtocolConnection(
                new TestSerialByteStream());

        await connection.DisposeAsync();

        async Task Act()
        {
            _ = await connection.ExchangeAsync(
                CreateRequest());
        }

        await Assert.ThrowsAsync<ObjectDisposedException>(
            Act);
    }

    [Fact]
    public void Constructor_NullStream_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactSerialProtocolConnection(
                null!);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    private static CompactSerialFrame CreateRequest()
    {
        return new CompactSerialFrame(
            messageType: 0x01,
            correlationId: 0x21,
            payload: []);
    }

    private sealed class TestSerialByteStream
        : ISerialByteStream
    {
        private readonly byte[] _readBytes;
        private int _readPosition;

        public TestSerialByteStream(
            byte[]? readBytes = null)
        {
            _readBytes =
                readBytes
                ?? [];
        }

        public List<byte[]> Writes
        {
            get;
        } =
            [];

        public Exception? WriteException
        {
            get;
            init;
        }

        public int DisposeCallCount
        {
            get;
            private set;
        }

        public ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int available =
                _readBytes.Length
                - _readPosition;

            if (available == 0)
            {
                return ValueTask.FromResult(
                    0);
            }

            int bytesToRead =
                Math.Min(
                    available,
                    buffer.Length);

            _readBytes.AsMemory(
                    _readPosition,
                    bytesToRead)
                .CopyTo(
                    buffer);

            _readPosition +=
                bytesToRead;

            return ValueTask.FromResult(
                bytesToRead);
        }

        public ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (WriteException is not null)
            {
                throw WriteException;
            }

            Writes.Add(
                buffer.ToArray());

            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCallCount++;

            return ValueTask.CompletedTask;
        }
    }
}