using System.Net;
using System.Net.Sockets;
using Hase.Transport.Tcp;

namespace Hase.Transport.Tests;

public sealed class TcpTransportConnectionSuccessfulTraceTests
{
    [Fact]
    public async Task ExchangeAsync_Success_ShouldPublishCompletedTrace()
    {
        byte[] request =
        [
            0x01,
            0x02,
            0x03,
            0x04
        ];

        byte[] response =
        [
            0x10,
            0x20,
            0x30
        ];

        DateTimeOffset startedAtUtc =
            DateTimeOffset.FromUnixTimeMilliseconds(
                1_750_000_000_000);

        var timeProvider =
            new TestTimeProvider(
                startedAtUtc);

        await using var server =
            new FramedTcpTestServer();

        Task serverTask =
            server.ServeSingleExchangeAsync(
                (
                    receivedRequest,
                    cancellationToken) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    Assert.Equal(
                        request,
                        receivedRequest);

                    timeProvider.Advance(
                        TimeSpan.FromMilliseconds(
                            125));

                    return Task.FromResult(
                        response);
                });

        using var client =
            new TcpClient();

        await client.ConnectAsync(
            IPAddress.Loopback,
            server.Port);

        await using var connection =
            new TcpTransportConnection(
                client,
                maximumPayloadLength:
                    1024,
                timeProvider);

        var observer =
            new TestTraceObserver();

        connection.SubscribeTrace(
            observer);

        byte[] actualResponse =
            await connection.ExchangeAsync(
                request);

        await serverTask;

        Assert.Equal(
            response,
            actualResponse);

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
            request.Length,
            trace.RequestByteCount);

        Assert.Equal(
            response.Length,
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
    }

    [Fact]
    public async Task ExchangeAsync_AfterUnsubscribe_ShouldNotPublishTrace()
    {
        byte[] request =
        [
            0x01
        ];

        byte[] response =
        [
            0x02
        ];

        await using var server =
            new FramedTcpTestServer();

        Task serverTask =
            server.ServeSingleExchangeAsync(
                (
                    receivedRequest,
                    cancellationToken) =>
                {
                    return Task.FromResult(
                        response);
                });

        using var client =
            new TcpClient();

        await client.ConnectAsync(
            IPAddress.Loopback,
            server.Port);

        await using var connection =
            new TcpTransportConnection(
                client,
                maximumPayloadLength:
                    1024);

        var observer =
            new TestTraceObserver();

        connection.SubscribeTrace(
            observer);

        connection.UnsubscribeTrace(
            observer);

        await connection.ExchangeAsync(
            request);

        await serverTask;

        Assert.Empty(
            observer.Traces);
    }

    private sealed class TestTimeProvider
        : TimeProvider
    {
        private DateTimeOffset _utcNow;
        private long _timestamp;

        public TestTimeProvider(
            DateTimeOffset utcNow)
        {
            if (utcNow.Offset != TimeSpan.Zero)
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
            return _utcNow;
        }

        public override long GetTimestamp()
        {
            return _timestamp;
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

    private sealed class TestTraceObserver
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
}