using Hase.Core.Domain.Identity;
using Hase.Protocol;
using Hase.Transport;
using System.Threading.Channels;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class ProtocolDuplexSessionCancellationTraceTests
{
    [Fact]
    public async Task SendAsync_Cancelled_ShouldPublishOneTraceAndIgnoreLateResponse()
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

        using var runCancellationTokenSource =
            new CancellationTokenSource();

        Task runTask =
            session.RunAsync(
                runCancellationTokenSource.Token);

        using var requestCancellationTokenSource =
            new CancellationTokenSource();

        var correlationId =
            new CorrelationId(
                1101);

        Task<ProtocolMessage> responseTask =
            session.SendAsync(
                new DiscoverRequest(
                    correlationId),
                requestCancellationTokenSource.Token);

        byte[] requestFrame =
            await connection.ReadSentFrameAsync();

        timeProvider.Advance(
            TimeSpan.FromMilliseconds(
                75));

        // Act
        requestCancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(
                async () => await responseTask);

        // Assert
        TransportExchangeTrace cancelledTrace =
            Assert.Single(
                observer.Traces);

        Assert.Equal(
            1,
            cancelledTrace.SequenceNumber);

        Assert.Equal(
            startedAtUtc,
            cancelledTrace.StartedAtUtc);

        Assert.Equal(
            startedAtUtc.AddMilliseconds(
                75),
            cancelledTrace.CompletedAtUtc);

        Assert.Equal(
            TimeSpan.FromMilliseconds(
                75),
            cancelledTrace.Duration);

        Assert.Equal(
            requestFrame.Length,
            cancelledTrace.RequestByteCount);

        Assert.Equal(
            0,
            cancelledTrace.ResponseByteCount);

        Assert.Equal(
            TransportExchangeOutcome.Cancelled,
            cancelledTrace.Outcome);

        Assert.Equal(
            typeof(TaskCanceledException).FullName,
            cancelledTrace.ExceptionType);

        Assert.NotNull(
            cancelledTrace.ExceptionMessage);

        connection.QueueReceivedMessage(
            new DiscoverResponse(
                correlationId,
                new EndpointId(
                    "Late-Endpoint"),
                []));

        var continuingCorrelationId =
            new CorrelationId(
                1102);

        Task<ProtocolMessage> continuingTask =
            session.SendAsync(
                new DiscoverRequest(
                    continuingCorrelationId));

        _ =
            await connection.ReadSentFrameAsync();

        connection.QueueReceivedMessage(
            new DiscoverResponse(
                continuingCorrelationId,
                new EndpointId(
                    "Continuing-Endpoint"),
                []));

        _ =
            await continuingTask;

        Assert.Equal(
            2,
            observer.Traces.Count);

        Assert.Equal(
            TransportExchangeOutcome.Cancelled,
            observer.Traces[0].Outcome);

        Assert.Equal(
            TransportExchangeOutcome.Succeeded,
            observer.Traces[1].Outcome);

        await StopAsync(
            runCancellationTokenSource,
            runTask);
    }

    [Fact]
    public async Task RunAsync_CancelledWithPendingRequest_ShouldPublishCancelledTrace()
    {
        // Arrange
        var connection =
            new TestDuplexTransportConnection();

        var session =
            new ProtocolDuplexSession(
                connection);

        var observer =
            new RecordingTraceObserver();

        session.SubscribeTrace(
            observer);

        using var runCancellationTokenSource =
            new CancellationTokenSource();

        Task runTask =
            session.RunAsync(
                runCancellationTokenSource.Token);

        Task<ProtocolMessage> responseTask =
            session.SendAsync(
                new DiscoverRequest(
                    new CorrelationId(
                        1201)));

        byte[] requestFrame =
            await connection.ReadSentFrameAsync();

        // Act
        runCancellationTokenSource.Cancel();

        // Assert
        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(
                async () => await runTask);

        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(
                async () => await responseTask);

        TransportExchangeTrace trace =
            Assert.Single(
                observer.Traces);

        Assert.Equal(
            requestFrame.Length,
            trace.RequestByteCount);

        Assert.Equal(
            0,
            trace.ResponseByteCount);

        Assert.Equal(
            TransportExchangeOutcome.Cancelled,
            trace.Outcome);

        Assert.Equal(
            typeof(OperationCanceledException).FullName,
            trace.ExceptionType);

        Assert.False(
            session.IsRunning);
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
            Traces.Add(
                trace);
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
            throw new NotSupportedException();
        }

        public async Task SendAsync(
            byte[] payload,
            CancellationToken cancellationToken = default)
        {
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

        public void QueueReceivedMessage(
            ProtocolMessage message)
        {
            ProtocolEnvelope envelope =
                _payloadCodec.Encode(
                    message);

            byte[] frame =
                _envelopeByteCodec.Encode(
                    envelope);

            Assert.True(
                _receivedFrames.Writer.TryWrite(
                    frame));
        }
    }
}