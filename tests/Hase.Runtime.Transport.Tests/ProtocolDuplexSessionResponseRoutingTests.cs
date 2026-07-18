using Hase.Core.Domain.Identity;
using Hase.Protocol;
using Hase.Transport;
using System.Threading.Channels;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class ProtocolDuplexSessionResponseRoutingTests
{
    [Fact]
    public async Task SendAsync_ShouldEncodeRequestAndRouteResponse()
    {
        // Arrange
        var connection =
            new TestDuplexTransportConnection();

        var session =
            new ProtocolDuplexSession(
                connection);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task runTask =
            session.RunAsync(
                cancellationTokenSource.Token);

        var correlationId =
            new CorrelationId(
                101);

        var request =
            new DiscoverRequest(
                correlationId);

        Task<ProtocolMessage> responseTask =
            session.SendAsync(
                request);

        // Act
        byte[] sentFrame =
            await connection.ReadSentFrameAsync();

        ProtocolMessage sentMessage =
            Decode(
                sentFrame);

        var expectedResponse =
            new DiscoverResponse(
                correlationId,
                new EndpointId(
                    "Endpoint-1"),
                [
                    new InstrumentId(
                        "Instrument-1")
                ]);

        connection.QueueReceivedMessage(
            expectedResponse);

        ProtocolMessage actualResponse =
            await responseTask;

        // Assert
        DiscoverRequest sentRequest =
            Assert.IsType<DiscoverRequest>(
                sentMessage);

        Assert.Equal(
            correlationId,
            sentRequest.CorrelationId);

        DiscoverResponse discoverResponse =
            Assert.IsType<DiscoverResponse>(
                actualResponse);

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

        await StopAsync(
            cancellationTokenSource,
            runTask);
    }

    [Fact]
    public async Task SendAsync_ConcurrentResponsesOutOfOrder_ShouldRouteByCorrelationId()
    {
        // Arrange
        var connection =
            new TestDuplexTransportConnection();

        var session =
            new ProtocolDuplexSession(
                connection);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task runTask =
            session.RunAsync(
                cancellationTokenSource.Token);

        var firstCorrelationId =
            new CorrelationId(
                201);

        var secondCorrelationId =
            new CorrelationId(
                202);

        Task<ProtocolMessage> firstResponseTask =
            session.SendAsync(
                new DiscoverRequest(
                    firstCorrelationId));

        Task<ProtocolMessage> secondResponseTask =
            session.SendAsync(
                new DiscoverRequest(
                    secondCorrelationId));

        byte[] firstSentFrame =
            await connection.ReadSentFrameAsync();

        byte[] secondSentFrame =
            await connection.ReadSentFrameAsync();

        ProtocolMessage firstSentMessage =
            Decode(
                firstSentFrame);

        ProtocolMessage secondSentMessage =
            Decode(
                secondSentFrame);

        HashSet<CorrelationId> sentCorrelationIds =
        [
            firstSentMessage.CorrelationId,
            secondSentMessage.CorrelationId
        ];

        Assert.Equal(
            new HashSet<CorrelationId>
            {
                firstCorrelationId,
                secondCorrelationId
            },
            sentCorrelationIds);

        // Act
        connection.QueueReceivedMessage(
            new DiscoverResponse(
                secondCorrelationId,
                new EndpointId(
                    "Endpoint-2"),
                []));

        connection.QueueReceivedMessage(
            new DiscoverResponse(
                firstCorrelationId,
                new EndpointId(
                    "Endpoint-1"),
                []));

        ProtocolMessage firstResponse =
            await firstResponseTask;

        ProtocolMessage secondResponse =
            await secondResponseTask;

        // Assert
        Assert.Equal(
            firstCorrelationId,
            firstResponse.CorrelationId);

        Assert.Equal(
            secondCorrelationId,
            secondResponse.CorrelationId);

        Assert.Equal(
            new EndpointId(
                "Endpoint-1"),
            Assert.IsType<DiscoverResponse>(
                    firstResponse)
                .EndpointId);

        Assert.Equal(
            new EndpointId(
                "Endpoint-2"),
            Assert.IsType<DiscoverResponse>(
                    secondResponse)
                .EndpointId);

        await StopAsync(
            cancellationTokenSource,
            runTask);
    }

    [Fact]
    public async Task SendAsync_DuplicatePendingCorrelationId_ShouldThrow()
    {
        // Arrange
        var connection =
            new TestDuplexTransportConnection();

        var session =
            new ProtocolDuplexSession(
                connection);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        Task runTask =
            session.RunAsync(
                cancellationTokenSource.Token);

        var correlationId =
            new CorrelationId(
                301);

        Task<ProtocolMessage> firstResponseTask =
            session.SendAsync(
                new DiscoverRequest(
                    correlationId));

        _ =
            await connection.ReadSentFrameAsync();

        // Act
        Task<ProtocolMessage> SendDuplicate()
        {
            return session.SendAsync(
                new DiscoverRequest(
                    correlationId));
        }

        InvalidOperationException exception =
            await Assert.ThrowsAsync<
                InvalidOperationException>(
                    SendDuplicate);

        connection.QueueReceivedMessage(
            new DiscoverResponse(
                correlationId,
                new EndpointId(
                    "Endpoint-1"),
                []));

        ProtocolMessage firstResponse =
            await firstResponseTask;

        // Assert
        Assert.Equal(
            "Correlation identifier '301' is already pending "
            + "in this protocol session.",
            exception.Message);

        Assert.Equal(
            correlationId,
            firstResponse.CorrelationId);

        await StopAsync(
            cancellationTokenSource,
            runTask);
    }

    private static ProtocolMessage Decode(
        byte[] frame)
    {
        var envelopeCodec =
            new ProtocolEnvelopeByteCodec();

        var payloadCodec =
            new BinaryProtocolPayloadCodec();

        ProtocolEnvelope envelope =
            envelopeCodec.Decode(
                frame);

        return payloadCodec.Decode(
            envelope);
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

        public async Task<byte[]> ReadSentFrameAsync()
        {
            return await _sentFrames.Reader.ReadAsync();
        }

        public void QueueReceivedMessage(
            ProtocolMessage message)
        {
            ArgumentNullException.ThrowIfNull(
                message);

            bool written =
                _receivedFrames.Writer.TryWrite(
                    Encode(
                        message));

            Assert.True(
                written);
        }
    }
}
