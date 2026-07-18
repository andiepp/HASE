using Hase.Core.Domain.Identity;
using Hase.Protocol;
using Hase.Transport;
using System.Threading.Channels;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class ProtocolDuplexSessionSuccessfulTraceTests
{
    [Fact]
    public async Task SendAsync_Success_ShouldPublishCompletedTrace()
    {
        // Arrange
        DateTimeOffset startedAtUtc =
            DateTimeOffset.FromUnixTimeMilliseconds(
                1_750_000_000_000);

        var timeProvider =
            new TestTimeProvider(
                startedAtUtc);

        var connection =
            new TestDuplexTransportConnection();

        var session =
            new ProtocolDuplexSession(
                connection,
                timeProvider);

        var observer =
            new RecordingTraceObserver();

        session.SubscribeTrace(
            observer);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task runTask =
            session.RunAsync(
                cancellationTokenSource.Token);

        var correlationId =
            new CorrelationId(
                901);

        Task<ProtocolMessage> responseTask =
            session.SendAsync(
                new DiscoverRequest(
                    correlationId));

        byte[] requestFrame =
            await connection.ReadSentFrameAsync();

        timeProvider.Advance(
            TimeSpan.FromMilliseconds(
                125));

        var response =
            new DiscoverResponse(
                correlationId,
                new EndpointId(
                    "Endpoint-1"),
                [
                    new InstrumentId(
                        "Instrument-1")
                ]);

        int responseByteCount =
            connection.QueueReceivedMessage(
                response);

        // Act
        ProtocolMessage actualResponse =
            await responseTask;

        // Assert
        DiscoverResponse discoverResponse =
            Assert.IsType<DiscoverResponse>(
                actualResponse);

        Assert.Equal(
            correlationId,
            discoverResponse.CorrelationId);

        Assert.Equal(
            new EndpointId(
                "Endpoint-1"),
            discoverResponse.EndpointId);

        Assert.Equal(
            [
                new InstrumentId(
                    "Instrument-1")
            ],
            discoverResponse.InstrumentIds);

        TransportExchangeTrace trace =
            Assert.Single(
                observer.Traces);

        Assert.Equal(
            1,
            trace.SequenceNumber);

        Assert.Equal(
            startedAtUtc,
            trace.StartedAtUtc);

        Assert.Equal(
            startedAtUtc.AddMilliseconds(
                125),
            trace.CompletedAtUtc);

        Assert.Equal(
            TimeSpan.FromMilliseconds(
                125),
            trace.Duration);

        Assert.Equal(
            requestFrame.Length,
            trace.RequestByteCount);

        Assert.Equal(
            responseByteCount,
            trace.ResponseByteCount);

        Assert.Equal(
            TransportExchangeOutcome.Succeeded,
            trace.Outcome);

        Assert.Equal(
            TransportConnectionState.Connected,
            trace.ConnectionState);

        Assert.Null(
            trace.ExceptionType);

        Assert.Null(
            trace.ExceptionMessage);

        await StopAsync(
            cancellationTokenSource,
            runTask);
    }

    [Fact]
    public async Task SendAsync_TraceObserverThrows_ShouldContinueAndPreserveSequence()
    {
        // Arrange
        var connection =
            new TestDuplexTransportConnection();

        var session =
            new ProtocolDuplexSession(
                connection);

        var throwingObserver =
            new ThrowingTraceObserver();

        var recordingObserver =
            new RecordingTraceObserver();

        session.SubscribeTrace(
            throwingObserver);

        session.SubscribeTrace(
            recordingObserver);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task runTask =
            session.RunAsync(
                cancellationTokenSource.Token);

        // Act
        await CompleteExchangeAsync(
            session,
            connection,
            correlationIdValue:
                902,
            endpointId:
                "Endpoint-1");

        await CompleteExchangeAsync(
            session,
            connection,
            correlationIdValue:
                903,
            endpointId:
                "Endpoint-2");

        // Assert
        Assert.Equal(
            2,
            throwingObserver.NotificationCount);

        Assert.Collection(
            recordingObserver.Traces,
            first =>
            {
                Assert.Equal(
                    1,
                    first.SequenceNumber);

                Assert.Equal(
                    TransportExchangeOutcome.Succeeded,
                    first.Outcome);
            },
            second =>
            {
                Assert.Equal(
                    2,
                    second.SequenceNumber);

                Assert.Equal(
                    TransportExchangeOutcome.Succeeded,
                    second.Outcome);
            });

        Assert.True(
            session.IsRunning);

        await StopAsync(
            cancellationTokenSource,
            runTask);
    }

    private static async Task CompleteExchangeAsync(
        ProtocolDuplexSession session,
        TestDuplexTransportConnection connection,
        uint correlationIdValue,
        string endpointId)
    {
        var correlationId =
            new CorrelationId(
                correlationIdValue);

        Task<ProtocolMessage> responseTask =
            session.SendAsync(
                new DiscoverRequest(
                    correlationId));

        _ =
            await connection.ReadSentFrameAsync();

        connection.QueueReceivedMessage(
            new DiscoverResponse(
                correlationId,
                new EndpointId(
                    endpointId),
                []));

        ProtocolMessage response =
            await responseTask;

        Assert.Equal(
            correlationId,
            response.CorrelationId);
    }

    private static async Task StopAsync(
        CancellationTokenSource cancellationTokenSource,
        Task runTask)
    {
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(
                async () => await runTask);
    }

    private sealed class TestTimeProvider
        : TimeProvider
    {
        private DateTimeOffset _utcNow;
        private long _timestamp;

        public TestTimeProvider(
            DateTimeOffset utcNow)
        {
            if (utcNow.Offset
                != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "The initial time must be expressed in UTC.",
                    nameof(utcNow));
            }

            _utcNow =
                utcNow;
        }

        public override long TimestampFrequency =>
            TimeSpan.TicksPerSecond;

        public override DateTimeOffset GetUtcNow()
        {
            return
                _utcNow;
        }

        public override long GetTimestamp()
        {
            return
                _timestamp;
        }

        public void Advance(
            TimeSpan duration)
        {
            if (duration < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(duration));
            }

            _utcNow =
                _utcNow.Add(
                    duration);

            _timestamp +=
                duration.Ticks;
        }
    }

    private sealed class RecordingTraceObserver
        : ITransportExchangeTraceObserver
    {
        public List<TransportExchangeTrace> Traces
        {
            get;
        } = [];

        public void OnTransportExchangeCompleted(
            TransportExchangeTrace trace)
        {
            ArgumentNullException.ThrowIfNull(
                trace);

            Traces.Add(
                trace);
        }
    }

    private sealed class ThrowingTraceObserver
        : ITransportExchangeTraceObserver
    {
        public int NotificationCount
        {
            get;
            private set;
        }

        public void OnTransportExchangeCompleted(
            TransportExchangeTrace trace)
        {
            ArgumentNullException.ThrowIfNull(
                trace);

            NotificationCount++;

            throw new InvalidOperationException(
                "Expected trace-observer failure.");
        }
    }

    private sealed class TestDuplexTransportConnection
        : ITransportDuplexConnection
    {
        private readonly Channel<byte[]> _sentFrames =
            Channel.CreateUnbounded<byte[]>();

        private readonly Channel<byte[]> _receivedFrames =
            Channel.CreateUnbounded<byte[]>();

        private readonly BinaryProtocolPayloadCodec _payloadCodec =
            new();

        private readonly ProtocolEnvelopeByteCodec _envelopeByteCodec =
            new();

        public event EventHandler<
            TransportConnectionStateChangedEventArgs>?
            StateChanged
        {
            add
            {
            }

            remove
            {
            }
        }

        public TransportConnectionState State =>
            TransportConnectionState.Connected;

        public Task<byte[]> ExchangeAsync(
            byte[] request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException(
                "The test connection uses duplex operations.");
        }

        public async Task SendAsync(
            byte[] payload,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                payload);

            cancellationToken.ThrowIfCancellationRequested();

            await _sentFrames.Writer.WriteAsync(
                payload,
                cancellationToken);
        }

        public async Task<byte[]> ReceiveAsync(
            CancellationToken cancellationToken = default)
        {
            return await _receivedFrames.Reader.ReadAsync(
                cancellationToken);
        }

        public async Task<byte[]> ReadSentFrameAsync()
        {
            return await _sentFrames.Reader.ReadAsync();
        }

        public int QueueReceivedMessage(
            ProtocolMessage message)
        {
            ArgumentNullException.ThrowIfNull(
                message);

            ProtocolEnvelope envelope =
                _payloadCodec.Encode(
                    message);

            byte[] frame =
                _envelopeByteCodec.Encode(
                    envelope);

            bool written =
                _receivedFrames.Writer.TryWrite(
                    frame);

            Assert.True(
                written);

            return
                frame.Length;
        }
    }
}