using Hase.Protocol;
using Hase.Transport;
using System.Threading.Channels;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class ProtocolDuplexSessionReceiveFailureTraceTests
{
    [Fact]
    public async Task RunAsync_ReceiveFailureWithPendingRequest_ShouldPublishFailedTrace()
    {
        // Arrange
        var expectedException =
            new InvalidDataException(
                "Expected receive failure.");

        var connection =
            new TestDuplexTransportConnection();

        var session =
            new ProtocolDuplexSession(
                connection);

        var observer =
            new RecordingTraceObserver();

        session.SubscribeTrace(
            observer);

        Task runTask =
            session.RunAsync();

        var request =
            new DiscoverRequest(
                new CorrelationId(
                    1401));

        Task<ProtocolMessage> responseTask =
            session.SendAsync(
                request);

        byte[] requestFrame =
            await connection.ReadSentFrameAsync();

        // Act
        connection.FailReceive(
            expectedException);

        // Assert
        InvalidDataException runException =
            await Assert.ThrowsAsync<
                InvalidDataException>(
                    async () => await runTask);

        InvalidDataException responseException =
            await Assert.ThrowsAsync<
                InvalidDataException>(
                    async () => await responseTask);

        Assert.Same(
            expectedException,
            runException);

        Assert.Same(
            expectedException,
            responseException);

        TransportExchangeTrace trace =
            Assert.Single(
                observer.Traces);

        Assert.Equal(
            1,
            trace.SequenceNumber);

        Assert.Equal(
            requestFrame.Length,
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
            "Expected receive failure.",
            trace.ExceptionMessage);

        Assert.False(
            session.IsRunning);
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

        private readonly TaskCompletionSource<byte[]> _receiveCompletionSource =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        private TransportConnectionState _state =
            TransportConnectionState.Connected;

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

        public Task<byte[]> ReceiveAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return _receiveCompletionSource.Task.WaitAsync(
                cancellationToken);
        }

        public async Task<byte[]> ReadSentFrameAsync()
        {
            return await _sentFrames.Reader.ReadAsync();
        }

        public void FailReceive(
            Exception exception)
        {
            ArgumentNullException.ThrowIfNull(
                exception);

            TransitionTo(
                TransportConnectionState.Faulted);

            bool completed =
                _receiveCompletionSource.TrySetException(
                    exception);

            Assert.True(
                completed);
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
