using Hase.Core.Domain.Identity;
using Hase.Protocol;
using Hase.Transport;
using System.Threading.Channels;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class DuplexRuntimeProtocolConnectionTests
{
    [Fact]
    public void Constructor_NullSession_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = new DuplexRuntimeProtocolConnection(
                null!);
        }

        // Assert
        ArgumentNullException exception =
            Assert.Throws<
                ArgumentNullException>(
                    Act);

        Assert.Equal(
            "session",
            exception.ParamName);
    }

    [Fact]
    public async Task SendAsync_ShouldDelegateToSession()
    {
        // Arrange
        var transportConnection =
            new TestDuplexTransportConnection();

        var session =
            new ProtocolDuplexSession(
                transportConnection);

        var connection =
            new DuplexRuntimeProtocolConnection(
                session);

        Assert.Same(
            session,
            connection.Session);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task runTask =
            session.RunAsync(
                cancellationTokenSource.Token);

        var correlationId =
            new CorrelationId(
                1601);

        // Act
        Task<ProtocolMessage> responseTask =
            connection.SendAsync(
                new DiscoverRequest(
                    correlationId));

        ProtocolMessage sentRequest =
            await transportConnection.ReadSentMessageAsync();

        transportConnection.QueueReceivedMessage(
            new DiscoverResponse(
                correlationId,
                new EndpointId(
                    "Endpoint-1"),
                [
                    new InstrumentId(
                        "Instrument-1")
                ]));

        ProtocolMessage response =
            await responseTask;

        // Assert
        DiscoverRequest discoverRequest =
            Assert.IsType<DiscoverRequest>(
                sentRequest);

        Assert.Equal(
            correlationId,
            discoverRequest.CorrelationId);

        DiscoverResponse discoverResponse =
            Assert.IsType<DiscoverResponse>(
                response);

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

            Assert.True(
                _receivedFrames.Writer.TryWrite(
                    frame));
        }
    }
}