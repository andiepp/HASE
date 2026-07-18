using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Protocol;
using Hase.Transport;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class LegacyRuntimeProtocolConnectionExchangeTests
{
    [Fact]
    public async Task SendAsync_ValidResponse_ShouldReturnDecodedMessage()
    {
        // Arrange
        var correlationId =
            new CorrelationId(
                1501);

        var expectedResponse =
            new DiscoverResponse(
                correlationId,
                new EndpointId(
                    "Endpoint-1"),
                [
                    new InstrumentId(
                        "Instrument-1")
                ]);

        var transportConnection =
            new TestTransportConnection(
                expectedResponse);

        var connection =
            new LegacyRuntimeProtocolConnection(
                transportConnection);

        var request =
            new DiscoverRequest(
                correlationId);

        // Act
        ProtocolMessage response =
            await connection.SendAsync(
                request);

        // Assert
        DiscoverRequest sentRequest =
            Assert.IsType<DiscoverRequest>(
                transportConnection.SentMessage);

        Assert.Equal(
            correlationId,
            sentRequest.CorrelationId);

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
    }

    [Fact]
    public async Task SendAsync_NonResponseMessage_ShouldThrow()
    {
        // Arrange
        var notification =
            new EventNotification(
                new InstrumentId(
                    "controller-01"),
                new DescriptorPath(
                    "Controller",
                    "ButtonPressed"),
                new DateTimeOffset(
                    2026,
                    7,
                    18,
                    6,
                    0,
                    0,
                    TimeSpan.Zero),
                null);

        var connection =
            new LegacyRuntimeProtocolConnection(
                new TestTransportConnection(
                    notification));

        // Act
        Task<ProtocolMessage> Act()
        {
            return connection.SendAsync(
                new DiscoverRequest(
                    new CorrelationId(
                        1502)));
        }

        // Assert
        InvalidDataException exception =
            await Assert.ThrowsAsync<
                InvalidDataException>(
                    Act);

        Assert.Equal(
            "The transport returned a non-response "
            + "protocol message.",
            exception.Message);
    }

    [Fact]
    public async Task SendAsync_MismatchedCorrelationId_ShouldThrow()
    {
        // Arrange
        var connection =
            new LegacyRuntimeProtocolConnection(
                new TestTransportConnection(
                    new DiscoverResponse(
                        new CorrelationId(
                            1599),
                        new EndpointId(
                            "Endpoint-1"),
                        [])));

        // Act
        Task<ProtocolMessage> Act()
        {
            return connection.SendAsync(
                new DiscoverRequest(
                    new CorrelationId(
                        1503)));
        }

        // Assert
        InvalidDataException exception =
            await Assert.ThrowsAsync<
                InvalidDataException>(
                    Act);

        Assert.Equal(
            "The protocol response correlation identifier "
            + "does not match its request.",
            exception.Message);
    }

    private sealed class TestTransportConnection
        : ITransportConnection
    {
        private readonly byte[] _responseFrame;

        private readonly BinaryProtocolPayloadCodec _payloadCodec =
            new();

        private readonly ProtocolEnvelopeByteCodec _envelopeByteCodec =
            new();

        public TestTransportConnection(
            ProtocolMessage response)
        {
            ProtocolEnvelope responseEnvelope =
                _payloadCodec.Encode(
                    response);

            _responseFrame =
                _envelopeByteCodec.Encode(
                    responseEnvelope);
        }

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

        public ProtocolMessage? SentMessage
        {
            get;
            private set;
        }

        public Task<byte[]> ExchangeAsync(
            byte[] request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                request);

            cancellationToken.ThrowIfCancellationRequested();

            ProtocolEnvelope requestEnvelope =
                _envelopeByteCodec.Decode(
                    request);

            SentMessage =
                _payloadCodec.Decode(
                    requestEnvelope);

            return Task.FromResult(
                _responseFrame);
        }
    }
}