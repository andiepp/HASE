using Hase.Protocol;
using Hase.Transport;
using System.Threading.Channels;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class ProtocolDuplexSessionSendFailureTraceTests
{
    [Fact]
    public async Task SendAsync_SendFailure_ShouldPublishFailedTrace()
    {
        // Arrange
        var expectedException =
            new InvalidOperationException(
                "Expected send failure.");

        var connection =
            new TestDuplexTransportConnection(
                expectedException);

        var session =
            new ProtocolDuplexSession(
                connection);

        var observer =
            new RecordingTraceObserver();

        session.SubscribeTrace(
            observer);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task runTask =
            session.RunAsync(
                cancellationTokenSource.Token);

        var request =
            new DiscoverRequest(
                new CorrelationId(
                    1301));

        byte[] expectedRequestFrame =
            Encode(
                request);

        // Act
        Task<ProtocolMessage> Act()
        {
            return session.SendAsync(
                request);
        }

        InvalidOperationException actualException =
            await Assert.ThrowsAsync<
                InvalidOperationException>(
                    Act);

        // Assert
        Assert.Same(
            expectedException,
            actualException);

        TransportExchangeTrace trace =
            Assert.Single(
                observer.Traces);

        Assert.Equal(
            1,
            trace.SequenceNumber);

        Assert.Equal(
            expectedRequestFrame.Length,
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
            typeof(InvalidOperationException).FullName,
            trace.ExceptionType);

        Assert.Equal(
            "Expected send failure.",
            trace.ExceptionMessage);

        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(
                async () => await runTask);
    }

    private static byte[] Encode(
        ProtocolMessage message)
    {
        var payloadCodec =
            new BinaryProtocolPayloadCodec();

        var envelopeCodec =
            new ProtocolEnvelopeByteCodec();

        ProtocolEnvelope envelope =
            payloadCodec.Encode(
                message);

        return envelopeCodec.Encode(
            envelope);
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
        private readonly Exception _sendException;

        private readonly Channel<byte[]> _receivedFrames =
            Channel.CreateUnbounded<byte[]>();

        private TransportConnectionState _state =
            TransportConnectionState.Connected;

        public TestDuplexTransportConnection(
            Exception sendException)
        {
            _sendException =
                sendException
                ?? throw new ArgumentNullException(
                    nameof(sendException));
        }

        public event EventHandler<
            TransportConnectionStateChangedEventArgs>?
            StateChanged;

        public TransportConnectionState State =>
            _state;

        public Task<byte[]> ExchangeAsync(
            byte[] request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException(
                "The test connection uses duplex operations.");
        }

        public Task SendAsync(
            byte[] payload,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                payload);

            cancellationToken.ThrowIfCancellationRequested();

            TransitionTo(
                TransportConnectionState.Faulted);

            return Task.FromException(
                _sendException);
        }

        public async Task<byte[]> ReceiveAsync(
            CancellationToken cancellationToken = default)
        {
            return await _receivedFrames.Reader.ReadAsync(
                cancellationToken);
        }

        private void TransitionTo(
            TransportConnectionState currentState)
        {
            TransportConnectionState previousState =
                _state;

            _state =
                currentState;

            StateChanged?.Invoke(
                this,
                new TransportConnectionStateChangedEventArgs(
                    previousState,
                    currentState));
        }
    }
}