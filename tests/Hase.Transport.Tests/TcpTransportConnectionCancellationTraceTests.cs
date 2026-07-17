using System.Net;
using System.Net.Sockets;
using Hase.Transport.Tcp;

namespace Hase.Transport.Tests;

public sealed class TcpTransportConnectionCancellationTraceTests
{
    [Fact]
    public async Task ExchangeAsync_CancelledAfterStart_ShouldPublishCancelledTrace()
    {
        byte[] request =
        [
            0x01,
            0x02,
            0x03
        ];

        DateTimeOffset startedAtUtc =
            DateTimeOffset.FromUnixTimeMilliseconds(
                1_750_000_000_000);

        var timeProvider =
            new TestTimeProvider(
                startedAtUtc);

        var requestReceived =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        using var serverCancellationTokenSource =
            new CancellationTokenSource();

        await using var server =
            new FramedTcpTestServer();

        Task serverTask =
            server.ServeSingleExchangeAsync(
                async (
                    receivedRequest,
                    cancellationToken) =>
                {
                    Assert.Equal(
                        request,
                        receivedRequest);

                    requestReceived.TrySetResult(
                        true);

                    await Task.Delay(
                        Timeout.InfiniteTimeSpan,
                        cancellationToken);

                    throw new InvalidOperationException(
                        "The server cancellation wait completed "
                        + "unexpectedly.");
                },
                serverCancellationTokenSource.Token);

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

        using var exchangeCancellationTokenSource =
            new CancellationTokenSource();

        Task<byte[]> exchangeTask =
            connection.ExchangeAsync(
                request,
                exchangeCancellationTokenSource.Token);

        await requestReceived.Task;

        timeProvider.Advance(
            TimeSpan.FromMilliseconds(
                80));

        exchangeCancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await exchangeTask);

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
                80),
            trace.CompletedAtUtc);

        Assert.Equal(
            TimeSpan.FromMilliseconds(
                80),
            trace.Duration);

        Assert.Equal(
            request.Length,
            trace.RequestByteCount);

        Assert.Equal(
            0,
            trace.ResponseByteCount);

        Assert.Equal(
            TransportExchangeOutcome.Cancelled,
            trace.Outcome);

        Assert.Equal(
            TransportConnectionState.Faulted,
            trace.ConnectionState);

        Assert.NotNull(
            trace.ExceptionType);

        Assert.Contains(
            "OperationCanceledException",
            trace.ExceptionType);

        serverCancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await serverTask);
    }

    [Fact]
    public async Task ExchangeAsync_PreCancelledToken_ShouldNotPublishTrace()
    {
        await using var server =
            new FramedTcpTestServer();

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

        using var cancellationTokenSource =
            new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () =>
                await connection.ExchangeAsync(
                    Array.Empty<byte>(),
                    cancellationTokenSource.Token));

        Assert.Empty(
            observer.Traces);

        Assert.Equal(
            TransportConnectionState.Connected,
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
}