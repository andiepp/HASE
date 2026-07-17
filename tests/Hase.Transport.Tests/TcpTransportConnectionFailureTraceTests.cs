using System.Net;
using System.Net.Sockets;
using Hase.Transport.Tcp;

namespace Hase.Transport.Tests;

public sealed class TcpTransportConnectionFailureTraceTests
{
    [Fact]
    public async Task ExchangeAsync_Failure_ShouldPublishTraceBeforePropagating()
    {
        byte[] request =
        [
            0x01,
            0x02
        ];

        byte[] oversizedResponse =
        [
            0x10,
            0x20,
            0x30,
            0x40,
            0x50
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
                    Assert.Equal(
                        request,
                        receivedRequest);

                    timeProvider.Advance(
                        TimeSpan.FromMilliseconds(
                            75));

                    return Task.FromResult(
                        oversizedResponse);
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
                    4,
                timeProvider);

        var observer =
            new TestTraceObserver();

        connection.SubscribeTrace(
            observer);

        InvalidDataException exception =
            await Assert.ThrowsAsync<InvalidDataException>(
                async () =>
                    await connection.ExchangeAsync(
                        request));

        await serverTask;

        Assert.Equal(
            TransportConnectionState.Faulted,
            connection.State);

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
                75),
            trace.CompletedAtUtc);

        Assert.Equal(
            TimeSpan.FromMilliseconds(
                75),
            trace.Duration);

        Assert.Equal(
            request.Length,
            trace.RequestByteCount);

        Assert.Equal(
            0,
            trace.ResponseByteCount);

        Assert.Equal(
            TransportExchangeOutcome.Failed,
            trace.Outcome);

        Assert.Equal(
            TransportConnectionState.Faulted,
            trace.ConnectionState);

        Assert.Equal(
            typeof(InvalidDataException).FullName,
            trace.ExceptionType);

        Assert.Equal(
            exception.Message,
            trace.ExceptionMessage);
    }

    [Fact]
    public async Task ExchangeAsync_FailureWithThrowingObserver_ShouldPreserveOriginalException()
    {
        byte[] oversizedResponse =
            new byte[5];

        await using var server =
            new FramedTcpTestServer();

        Task serverTask =
            server.ServeSingleExchangeAsync(
                (
                    receivedRequest,
                    cancellationToken) =>
                {
                    return Task.FromResult(
                        oversizedResponse);
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
                    4);

        connection.SubscribeTrace(
            new ThrowingTraceObserver());

        InvalidDataException exception =
            await Assert.ThrowsAsync<InvalidDataException>(
                async () =>
                    await connection.ExchangeAsync(
                        Array.Empty<byte>()));

        await serverTask;

        Assert.Contains(
            "exceeds the configured maximum",
            exception.Message);

        Assert.Equal(
            TransportConnectionState.Faulted,
            connection.State);
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

    private sealed class ThrowingTraceObserver
        : ITransportExchangeTraceObserver
    {
        public void OnTransportExchangeCompleted(
            TransportExchangeTrace trace)
        {
            throw new InvalidOperationException(
                "Trace observer failed.");
        }
    }
}