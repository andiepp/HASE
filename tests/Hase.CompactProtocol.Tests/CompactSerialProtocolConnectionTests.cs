using System.Threading.Channels;
using Hase.CompactProtocol;
using Hase.Transport;
using Hase.Transport.Serial;

namespace Hase.CompactProtocol.Tests;

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
    public async Task ExchangeAsync_EventBeforeResponse_ShouldDeliverEventAndCompleteResponse()
    {
        CompactSerialFrame notification =
            CompactEventNotificationCodec.Encode(
                new CompactEventNotification(
                    eventId: 0x01,
                    ReadOnlyMemory<byte>.Empty));

        var response =
            new CompactSerialFrame(
                messageType: 0x02,
                correlationId: 0x21,
                payload:
                [
                    0x30
                ]);

        byte[] input =
            CompactSerialFrameCodec.Encode(
                    notification)
                .Concat(
                    CompactSerialFrameCodec.Encode(
                        response))
                .ToArray();

        var stream =
            new TestSerialByteStream(
                input);

        await using var connection =
            new CompactSerialProtocolConnection(
                stream);

        var receivedNotifications =
            new List<CompactEventNotification>();

        ICompactSerialProtocolConnection abstraction =
            connection;

        abstraction.EventNotificationReceived +=
            notification =>
            {
                receivedNotifications.Add(
                    notification);
            };

        CompactSerialFrame actualResponse =
            await connection.ExchangeAsync(
                CreateRequest());

        CompactEventNotification actualNotification =
            Assert.Single(
                receivedNotifications);

        Assert.Equal(
            0x01,
            actualNotification.EventId);

        Assert.True(
            actualNotification.Value.IsEmpty);

        Assert.Equal(
            0x21,
            actualResponse.CorrelationId);

        Assert.Equal(
            TransportConnectionState.Connected,
            connection.State);
    }

    [Fact]
    public async Task ReceiveLoop_EventAfterResponse_ShouldContinueWithoutAnotherExchange()
    {
        var response =
            new CompactSerialFrame(
                messageType: 0x02,
                correlationId: 0x21,
                payload: []);

        var stream =
            new TestSerialByteStream(
                CompactSerialFrameCodec.Encode(
                    response));

        await using var connection =
            new CompactSerialProtocolConnection(
                stream);

        var receivedNotification =
            new TaskCompletionSource<CompactEventNotification>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        ICompactSerialProtocolConnection abstraction =
            connection;

        abstraction.EventNotificationReceived +=
            notification =>
            {
                receivedNotification.TrySetResult(
                    notification);
            };

        _ =
            await connection.ExchangeAsync(
                CreateRequest());

        CompactSerialFrame notification =
            CompactEventNotificationCodec.Encode(
                new CompactEventNotification(
                    eventId: 0x01,
                    ReadOnlyMemory<byte>.Empty));

        stream.EnqueueReadBytes(
            CompactSerialFrameCodec.Encode(
                notification));

        CompactEventNotification actual =
            await receivedNotification.Task.WaitAsync(
                TimeSpan.FromSeconds(
                    1));

        Assert.Equal(
            0x01,
            actual.EventId);

        Assert.True(
            actual.Value.IsEmpty);

        Assert.Equal(
            TransportConnectionState.Connected,
            connection.State);
    }

    [Fact]
    public async Task ReceiveLoop_MalformedEventNotification_ShouldFaultConnection()
    {
        var response =
            new CompactSerialFrame(
                messageType: 0x02,
                correlationId: 0x21,
                payload: []);

        var stream =
            new TestSerialByteStream(
                CompactSerialFrameCodec.Encode(
                    response));

        await using var connection =
            new CompactSerialProtocolConnection(
                stream);

        var faulted =
            CreateFaultedCompletion(
                connection);

        _ =
            await connection.ExchangeAsync(
                CreateRequest());

        stream.EnqueueReadBytes(
            CompactSerialFrameCodec.Encode(
                new CompactSerialFrame(
                    messageType:
                        (byte)CompactSerialMessageType.EventNotification,
                    correlationId: 0x00,
                    payload: [])));

        await faulted.Task.WaitAsync(
            TimeSpan.FromSeconds(
                1));

        Assert.Equal(
            TransportConnectionState.Faulted,
            connection.State);
    }

    [Fact]
    public async Task ReceiveLoop_ZeroCorrelationNonEvent_ShouldFaultConnection()
    {
        var response =
            new CompactSerialFrame(
                messageType: 0x02,
                correlationId: 0x21,
                payload: []);

        var stream =
            new TestSerialByteStream(
                CompactSerialFrameCodec.Encode(
                    response));

        await using var connection =
            new CompactSerialProtocolConnection(
                stream);

        var faulted =
            CreateFaultedCompletion(
                connection);

        _ =
            await connection.ExchangeAsync(
                CreateRequest());

        stream.EnqueueReadBytes(
            CompactSerialFrameCodec.Encode(
                new CompactSerialFrame(
                    messageType:
                        (byte)CompactSerialMessageType.ReadPropertyResponse,
                    correlationId: 0x00,
                    payload: [])));

        await faulted.Task.WaitAsync(
            TimeSpan.FromSeconds(
                1));

        Assert.Equal(
            TransportConnectionState.Faulted,
            connection.State);
    }

    [Fact]
    public async Task ExchangeAsync_NonzeroEventNotification_ShouldFaultConnection()
    {
        CompactSerialFrame invalidNotification =
            new(
                (byte)CompactSerialMessageType.EventNotification,
                correlationId: 0x21,
                payload:
                [
                    0x01
                ]);

        var stream =
            new TestSerialByteStream(
                CompactSerialFrameCodec.Encode(
                    invalidNotification));

        await using var connection =
            new CompactSerialProtocolConnection(
                stream);

        async Task Act()
        {
            _ =
                await connection.ExchangeAsync(
                    CreateRequest());
        }

        await Assert.ThrowsAsync<InvalidDataException>(
            Act);

        Assert.Equal(
            TransportConnectionState.Faulted,
            connection.State);
    }

    [Fact]
    public async Task ReceiveLoop_UnmatchedCorrelatedFrame_ShouldFaultConnection()
    {
        var response =
            new CompactSerialFrame(
                messageType: 0x02,
                correlationId: 0x21,
                payload: []);

        var stream =
            new TestSerialByteStream(
                CompactSerialFrameCodec.Encode(
                    response));

        await using var connection =
            new CompactSerialProtocolConnection(
                stream);

        var faulted =
            CreateFaultedCompletion(
                connection);

        _ =
            await connection.ExchangeAsync(
                CreateRequest());

        stream.EnqueueReadBytes(
            CompactSerialFrameCodec.Encode(
                new CompactSerialFrame(
                    messageType: 0x02,
                    correlationId: 0x22,
                    payload: [])));

        await faulted.Task.WaitAsync(
            TimeSpan.FromSeconds(
                1));

        Assert.Equal(
            TransportConnectionState.Faulted,
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
            new TestSerialByteStream(
                completeReads: true);

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

    private static TaskCompletionSource CreateFaultedCompletion(
        CompactSerialProtocolConnection connection)
    {
        var faulted =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        connection.StateChanged +=
            (
                sender,
                change) =>
            {
                if (change.CurrentState
                    == TransportConnectionState.Faulted)
                {
                    faulted.TrySetResult();
                }
            };

        return faulted;
    }

    private sealed class TestSerialByteStream
        : ISerialByteStream
    {
        private readonly Channel<byte> _readBytes =
            Channel.CreateUnbounded<byte>(
                new UnboundedChannelOptions
                {
                    SingleReader =
                        true,
                    SingleWriter =
                        false
                });

        public TestSerialByteStream(
            byte[]? readBytes = null,
            bool completeReads = false)
        {
            EnqueueReadBytes(
                readBytes
                ?? []);

            if (completeReads)
            {
                _readBytes.Writer.TryComplete();
            }
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

        public void EnqueueReadBytes(
            ReadOnlySpan<byte> bytes)
        {
            foreach (byte value in bytes)
            {
                if (!_readBytes.Writer.TryWrite(
                        value))
                {
                    throw new InvalidOperationException(
                        "The test serial read stream is already completed.");
                }
            }
        }

        public async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            byte first;

            try
            {
                first =
                    await _readBytes.Reader.ReadAsync(
                        cancellationToken);
            }
            catch (ChannelClosedException)
            {
                return 0;
            }

            buffer.Span[0] =
                first;

            int bytesRead =
                1;

            while (bytesRead < buffer.Length
                   && _readBytes.Reader.TryRead(
                       out byte value))
            {
                buffer.Span[bytesRead] =
                    value;

                bytesRead++;
            }

            return bytesRead;
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

            _readBytes.Writer.TryComplete();

            return ValueTask.CompletedTask;
        }
    }
}