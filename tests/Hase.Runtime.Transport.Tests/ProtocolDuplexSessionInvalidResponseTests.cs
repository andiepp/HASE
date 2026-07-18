using Hase.Core.Domain.Identity;
using Hase.Protocol;
using Hase.Transport;
using System.Threading.Channels;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class ProtocolDuplexSessionInvalidResponseTests
{
    [Fact]
    public async Task RunAsync_UnknownResponseCorrelationId_ShouldStopSession()
    {
        // Arrange
        var connection =
            new TestDuplexTransportConnection();

        var session =
            new ProtocolDuplexSession(
                connection);

        Task runTask =
            session.RunAsync();

        connection.QueueReceivedMessage(
            new DiscoverResponse(
                new CorrelationId(
                    701),
                new EndpointId(
                    "Endpoint-1"),
                []));

        // Act
        InvalidDataException exception =
            await Assert.ThrowsAsync<
                InvalidDataException>(
                    async () => await runTask);

        // Assert
        Assert.Equal(
            "Received a protocol response for unknown correlation "
            + "identifier '701'.",
            exception.Message);

        Assert.False(
            session.IsRunning);

        Task<ProtocolMessage> SendAfterFailure()
        {
            return session.SendAsync(
                new DiscoverRequest(
                    new CorrelationId(
                        702)));
        }

        InvalidOperationException sendException =
            await Assert.ThrowsAsync<
                InvalidOperationException>(
                    SendAfterFailure);

        Assert.Equal(
            "The protocol duplex session receive pump "
            + "is not running.",
            sendException.Message);
    }

    [Fact]
    public async Task RunAsync_ResponseWithZeroCorrelationId_ShouldStopSession()
    {
        // Arrange
        var connection =
            new TestDuplexTransportConnection();

        var session =
            new ProtocolDuplexSession(
                connection);

        Task runTask =
            session.RunAsync();

        connection.QueueReceivedMessage(
            new DiscoverResponse(
                CorrelationId.None,
                new EndpointId(
                    "Endpoint-1"),
                []));

        // Act
        InvalidDataException exception =
            await Assert.ThrowsAsync<
                InvalidDataException>(
                    async () => await runTask);

        // Assert
        Assert.Equal(
            "A protocol response received through a duplex "
            + "session must have a nonzero correlation identifier.",
            exception.Message);

        Assert.False(
            session.IsRunning);
    }

    private sealed class TestDuplexTransportConnection
        : ITransportDuplexConnection
    {
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

        public Task SendAsync(
            byte[] payload,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException(
                "No request is sent by these tests.");
        }

        public async Task<byte[]> ReceiveAsync(
            CancellationToken cancellationToken = default)
        {
            return await _receivedFrames.Reader.ReadAsync(
                cancellationToken);
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