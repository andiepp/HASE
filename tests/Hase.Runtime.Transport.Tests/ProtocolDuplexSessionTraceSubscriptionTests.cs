using Hase.Core.Domain.Identity;
using Hase.Protocol;
using Hase.Transport;
using System.Threading.Channels;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class ProtocolDuplexSessionTraceSubscriptionTests
{
    [Fact]
    public void SubscribeTrace_NullObserver_ShouldThrow()
    {
        // Arrange
        var session =
            new ProtocolDuplexSession(
                new TestDuplexTransportConnection());

        // Act
        void Act()
        {
            session.SubscribeTrace(
                null!);
        }

        // Assert
        Assert.Throws<
            ArgumentNullException>(
                Act);
    }

    [Fact]
    public void UnsubscribeTrace_NullObserver_ShouldThrow()
    {
        // Arrange
        var session =
            new ProtocolDuplexSession(
                new TestDuplexTransportConnection());

        // Act
        void Act()
        {
            session.UnsubscribeTrace(
                null!);
        }

        // Assert
        Assert.Throws<
            ArgumentNullException>(
                Act);
    }

    [Fact]
    public async Task SubscribeTrace_SameObserverTwice_ShouldPublishOnce()
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

        session.SubscribeTrace(
            observer);

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
                1001);

        // Assert
        Assert.Single(
            observer.Traces);

        await StopAsync(
            cancellationTokenSource,
            runTask);
    }

    [Fact]
    public async Task UnsubscribeTrace_ShouldStopPublication()
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

        session.UnsubscribeTrace(
            observer);

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
                1002);

        // Assert
        Assert.Empty(
            observer.Traces);

        await StopAsync(
            cancellationTokenSource,
            runTask);
    }

    private static async Task CompleteExchangeAsync(
        ProtocolDuplexSession session,
        TestDuplexTransportConnection connection,
        uint correlationIdValue)
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
                    "Endpoint-1"),
                []));

        _ =
            await responseTask;
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

            bool written =
                _receivedFrames.Writer.TryWrite(
                    frame);

            Assert.True(
                written);
        }
    }
}