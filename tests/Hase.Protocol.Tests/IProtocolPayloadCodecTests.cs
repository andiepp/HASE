using Hase.Protocol;

namespace Hase.Protocol.Tests;

public sealed class IProtocolPayloadCodecTests
{
    private sealed class TestProtocolPayloadCodec : IProtocolPayloadCodec
    {
        public ProtocolMessage? MessageToReturn { get; init; }

        public ProtocolEnvelope? EnvelopeToReturn { get; init; }

        public ProtocolMessage? EncodedMessage { get; private set; }

        public ProtocolEnvelope? DecodedEnvelope { get; private set; }

        public ProtocolEnvelope Encode(ProtocolMessage message)
        {
            EncodedMessage = message;

            return EnvelopeToReturn
                ?? throw new InvalidOperationException(
                    "No envelope was configured.");
        }

        public ProtocolMessage Decode(ProtocolEnvelope envelope)
        {
            DecodedEnvelope = envelope;

            return MessageToReturn
                ?? throw new InvalidOperationException(
                    "No message was configured.");
        }
    }

    [Fact]
    public void Encode_AcceptsMessageAndReturnsEnvelope()
    {
        DiscoverRequest message =
            new(new CorrelationId(17));

        ProtocolEnvelope envelope = new(
            ProtocolVersion.Current,
            ProtocolMessageRole.Request,
            ProtocolMessageType.DiscoverRequest,
            new CorrelationId(17),
            ReadOnlyMemory<byte>.Empty);

        TestProtocolPayloadCodec codec = new()
        {
            EnvelopeToReturn = envelope
        };

        ProtocolEnvelope result =
            codec.Encode(message);

        Assert.Same(
            message,
            codec.EncodedMessage);

        Assert.Same(
            envelope,
            result);
    }

    [Fact]
    public void Decode_AcceptsEnvelopeAndReturnsMessage()
    {
        ProtocolEnvelope envelope = new(
            ProtocolVersion.Current,
            ProtocolMessageRole.Request,
            ProtocolMessageType.DiscoverRequest,
            new CorrelationId(17),
            ReadOnlyMemory<byte>.Empty);

        DiscoverRequest message =
            new(new CorrelationId(17));

        TestProtocolPayloadCodec codec = new()
        {
            MessageToReturn = message
        };

        ProtocolMessage result =
            codec.Decode(envelope);

        Assert.Same(
            envelope,
            codec.DecodedEnvelope);

        Assert.Same(
            message,
            result);
    }
}