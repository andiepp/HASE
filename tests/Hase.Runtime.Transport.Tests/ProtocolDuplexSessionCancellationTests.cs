using Hase.Core.Domain.Identity;
using Hase.Protocol;
using Hase.Transport;
using System.Threading.Channels;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class ProtocolDuplexSessionCancellationTests
{
    [Fact]
    public async Task SendAsync_CancelledRequest_ShouldIgnoreLateResponseAndAllowReuse()
    {
        // Arrange
        var connection =
            new TestDuplexTransportConnection();

        var session =
            new ProtocolDuplexSession(
                connection);

        using var runCancellationTokenSource =
            new CancellationTokenSource();

        Task runTask =
            session.RunAsync(
                runCancellationTokenSource.Token);

        var abandonedCorrelationId =
            new CorrelationId(
                501);

        using var requestCancellationTokenSource =
            new CancellationTokenSource();

        Task<ProtocolMessage> cancelledResponseTask =
            session.SendAsync(
                new DiscoverRequest(
                    abandonedCorrelationId),
                requestCancellationTokenSource.Token);

        _ =
            await connection.ReadSentMessageAsync();

        // Act
        requestCancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(
                async () => await cancelledResponseTask);

        connection.QueueReceivedMessage(
            new DiscoverResponse(
                abandonedCorrelationId,
                new EndpointId(
                    "Late-Endpoint"),
                []));

        var continuingCorrelationId =
            new CorrelationId(
                502);

        Task<ProtocolMessage> continuingResponseTask =
            session.SendAsync(
                new DiscoverRequest(
                    continuingCorrelationId));

        ProtocolMessage continuingSentMessage =
            await connection.ReadSentMessageAsync();

        connection.QueueReceivedMessage(
            new DiscoverResponse(
                continuingCorrelationId,
                new EndpointId(
                    "Continuing-Endpoint"),
                []));

        ProtocolMessage continuingResponse =
            await continuingResponseTask;

        Task<ProtocolMessage> reusedResponseTask =
            session.SendAsync(
                new DiscoverRequest(
                    abandonedCorrelationId));

        ProtocolMessage reusedSentMessage =
            await connection.ReadSentMessageAsync();

        connection.QueueReceivedMessage(
            new DiscoverResponse(
                abandonedCorrelationId,
                new EndpointId(
                    "Reused-Endpoint"),
                []));

        ProtocolMessage reusedResponse =
            await reusedResponseTask;

        // Assert
        Assert.Equal(
            continuingCorrelationId,
            continuingSentMessage.CorrelationId);

        Assert.Equal(
            new EndpointId(
                "Continuing-Endpoint"),
            Assert.IsType<DiscoverResponse>(
                    continuingResponse)
                .EndpointId);

        Assert.Equal(
            abandonedCorrelationId,
            reusedSentMessage.CorrelationId);

        Assert.Equal(
            new EndpointId(
                "Reused-Endpoint"),
            Assert.IsType<DiscoverResponse>(
                    reusedResponse)
                .EndpointId);

        Assert.True(
            session.IsRunning);

        await StopAsync(
            runCancellationTokenSource,
            runTask);
    }

    [Fact]
    public async Task RunAsync_StopWithPendingRequest_ShouldCancelPendingRequest()
    {
        // Arrange
        var connection =
            new TestDuplexTransportConnection();

        var session =
            new ProtocolDuplexSession(
                connection);

        using var runCancellationTokenSource =
            new CancellationTokenSource();

        Task runTask =
            session.RunAsync(
                runCancellationTokenSource.Token);

        Task<ProtocolMessage> responseTask =
            session.SendAsync(
                new DiscoverRequest(
                    new CorrelationId(
                        601)));

        _ =
            await connection.ReadSentMessageAsync();

        // Act
        runCancellationTokenSource.Cancel();

        // Assert
        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(
                async () => await runTask);

        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(
                async () => await responseTask);

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

        public async Task<ProtocolMessage> ReadSentMessageAsync()
        {
            byte[] frame =
                await _sentFrames.Reader.ReadAsync();

            ProtocolEnvelope envelope =
                _envelopeByteCodec.Decode(
                    frame);

            return _payloadCodec.Decode(
                envelope);
        }

        public void QueueReceivedMessage(
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
        }
    }
}
