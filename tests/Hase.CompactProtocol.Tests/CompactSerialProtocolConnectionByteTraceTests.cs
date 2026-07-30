using System.Threading.Channels;
using Hase.CompactProtocol;
using Hase.Transport;
using Hase.Transport.Serial;

namespace Hase.CompactProtocol.Tests;

public sealed class CompactSerialProtocolConnectionByteTraceTests
{
    [Fact]
    public async Task ExchangeAsync_PublishesExactRequestAndResponseFrames()
    {
        CompactSerialFrame response =
            CreateResponse();

        byte[] encodedResponse =
            CompactSerialFrameCodec.Encode(
                response);

        var stream =
            new TestSerialByteStream(
                encodedResponse);

        await using var connection =
            new CompactSerialProtocolConnection(
                stream);

        var observer =
            new RecordingObserver();

        connection.SubscribeByteTrace(
            observer);

        CompactSerialFrame request =
            CreateRequest();

        _ =
            await connection.ExchangeAsync(
                request);

        RecordedTrace outbound =
            Assert.Single(
                observer.Traces,
                trace =>
                    trace.Direction
                    == TransportByteDirection.Outbound);

        RecordedTrace inbound =
            Assert.Single(
                observer.Traces,
                trace =>
                    trace.Direction
                    == TransportByteDirection.Inbound);

        Assert.Equal(
            "33",
            outbound.CorrelationId);
        Assert.Equal(
            CompactSerialFrameCodec.Encode(
                request),
            outbound.Bytes);
        Assert.Equal(
            "33",
            inbound.CorrelationId);
        Assert.Equal(
            encodedResponse,
            inbound.Bytes);
    }

    [Fact]
    public async Task ExchangeAsync_NotificationBeforeResponse_PublishesExactFrames()
    {
        CompactSerialFrame notification =
            CompactEventNotificationCodec.Encode(
                new CompactEventNotification(
                    eventId: 1,
                    value: new byte[]
                    {
                        0xA5
                    }));

        CompactSerialFrame response =
            CreateResponse();

        byte[] encodedNotification =
            CompactSerialFrameCodec.Encode(
                notification);

        byte[] encodedResponse =
            CompactSerialFrameCodec.Encode(
                response);

        var stream =
            new TestSerialByteStream(
                encodedNotification
                    .Concat(
                        encodedResponse)
                    .ToArray());

        await using var connection =
            new CompactSerialProtocolConnection(
                stream);

        var observer =
            new RecordingObserver();

        connection.SubscribeByteTrace(
            observer);

        _ =
            await connection.ExchangeAsync(
                CreateRequest());

        Assert.Equal(
            3,
            observer.Traces.Count);

        RecordedTrace notificationTrace =
            Assert.Single(
                observer.Traces,
                trace =>
                    trace.Direction
                        == TransportByteDirection.Inbound
                    && trace.CorrelationId is null);

        Assert.Equal(
            TransportByteDirection.Inbound,
            notificationTrace.Direction);
        Assert.Null(
            notificationTrace.CorrelationId);
        Assert.Equal(
            encodedNotification,
            notificationTrace.Bytes);

        RecordedTrace responseTrace =
            Assert.Single(
                observer.Traces,
                trace =>
                    trace.Direction
                        == TransportByteDirection.Inbound
                    && trace.CorrelationId == "33");

        Assert.Equal(
            encodedResponse,
            responseTrace.Bytes);
    }

    [Fact]
    public async Task ThrowingObserver_DoesNotChangeExchange()
    {
        var stream =
            new TestSerialByteStream(
                CompactSerialFrameCodec.Encode(
                    CreateResponse()));

        await using var connection =
            new CompactSerialProtocolConnection(
                stream);

        connection.SubscribeByteTrace(
            new ThrowingObserver());

        CompactSerialFrame response =
            await connection.ExchangeAsync(
                CreateRequest());

        Assert.Equal(
            0x21,
            response.CorrelationId);
        Assert.Equal(
            TransportConnectionState.Connected,
            connection.State);
    }

    [Fact]
    public async Task DuplicateThenRemovedSubscription_ReceivesNoFrames()
    {
        var stream =
            new TestSerialByteStream(
                CompactSerialFrameCodec.Encode(
                    CreateResponse()));

        await using var connection =
            new CompactSerialProtocolConnection(
                stream);

        var observer =
            new RecordingObserver();

        connection.SubscribeByteTrace(
            observer);
        connection.SubscribeByteTrace(
            observer);
        connection.UnsubscribeByteTrace(
            observer);

        _ =
            await connection.ExchangeAsync(
                CreateRequest());

        Assert.Empty(
            observer.Traces);
    }

    private static CompactSerialFrame CreateRequest()
    {
        return new CompactSerialFrame(
            messageType: 0x01,
            correlationId: 0x21,
            payload:
            [
                0x10,
                0x20
            ]);
    }

    private static CompactSerialFrame CreateResponse()
    {
        return new CompactSerialFrame(
            messageType: 0x02,
            correlationId: 0x21,
            payload:
            [
                0x30,
                0x40
            ]);
    }

    private sealed class RecordingObserver
        : ITransportByteTraceObserver
    {
        public List<RecordedTrace> Traces
        {
            get;
        } =
        [];

        public void OnTransportBytes(
            TransportByteTrace trace)
        {
            Traces.Add(
                new RecordedTrace(
                    trace.Direction,
                    trace.Bytes.ToArray(),
                    trace.CorrelationId));
        }
    }

    private sealed record RecordedTrace(
        TransportByteDirection Direction,
        byte[] Bytes,
        string? CorrelationId);

    private sealed class ThrowingObserver
        : ITransportByteTraceObserver
    {
        public void OnTransportBytes(
            TransportByteTrace trace)
        {
            throw new InvalidOperationException(
                "observer failure");
        }
    }

    private sealed class TestSerialByteStream
        : ISerialByteStream
    {
        private readonly Channel<byte> readBytes =
            Channel.CreateUnbounded<byte>(
                new UnboundedChannelOptions
                {
                    SingleReader =
                        true,
                    SingleWriter =
                        false
                });

        public TestSerialByteStream(
            ReadOnlySpan<byte> bytes)
        {
            Enqueue(
                bytes);
        }

        public void Enqueue(
            ReadOnlySpan<byte> bytes)
        {
            foreach (byte value in bytes)
            {
                Assert.True(
                    readBytes.Writer.TryWrite(
                        value));
            }
        }

        public async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            byte first =
                await readBytes.Reader.ReadAsync(
                    cancellationToken);

            buffer.Span[0] =
                first;

            int count =
                1;

            while (count < buffer.Length &&
                   readBytes.Reader.TryRead(
                       out byte value))
            {
                buffer.Span[count] =
                    value;

                count++;
            }

            return count;
        }

        public ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            readBytes.Writer.TryComplete();

            return ValueTask.CompletedTask;
        }
    }
}
