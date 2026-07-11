using Hase.Protocol;

namespace Hase.Protocol.Tests;

public sealed class BinaryProtocolPayloadCodecTests
{
    private sealed record UnsupportedTestMessage(
        CorrelationId CorrelationId)
        : ProtocolMessage(
            ProtocolVersion.Current,
            ProtocolMessageRole.Request,
            ProtocolMessageType.ReadPropertyRequest,
            CorrelationId);

    [Fact]
    public void Encode_DiscoverRequest_CreatesExpectedEnvelope()
    {
        BinaryProtocolPayloadCodec codec = new();

        DiscoverRequest request =
            new(new CorrelationId(17));

        ProtocolEnvelope envelope =
            codec.Encode(request);

        Assert.Equal(
            ProtocolVersion.Current,
            envelope.Version);

        Assert.Equal(
            ProtocolMessageRole.Request,
            envelope.Role);

        Assert.Equal(
            ProtocolMessageType.DiscoverRequest,
            envelope.MessageType);

        Assert.Equal(
            new CorrelationId(17),
            envelope.CorrelationId);

        Assert.True(
            envelope.Payload.IsEmpty);
    }

    [Fact]
    public void Decode_DiscoverRequest_CreatesExpectedMessage()
    {
        BinaryProtocolPayloadCodec codec = new();

        ProtocolEnvelope envelope = new(
            ProtocolVersion.Current,
            ProtocolMessageRole.Request,
            ProtocolMessageType.DiscoverRequest,
            new CorrelationId(17),
            ReadOnlyMemory<byte>.Empty);

        ProtocolMessage message =
            codec.Decode(envelope);

        DiscoverRequest request =
            Assert.IsType<DiscoverRequest>(message);

        Assert.Equal(
            new CorrelationId(17),
            request.CorrelationId);
    }

    [Fact]
    public void Encode_UnsupportedMessage_ThrowsNotSupportedException()
    {
        BinaryProtocolPayloadCodec codec = new();

        UnsupportedTestMessage message =
            new(new CorrelationId(1));

        Assert.Throws<NotSupportedException>(
            () => codec.Encode(message));
    }

    [Fact]
    public void Decode_UnsupportedMessageType_ThrowsNotSupportedException()
    {
        BinaryProtocolPayloadCodec codec = new();

        ProtocolEnvelope envelope = new(
            ProtocolVersion.Current,
            ProtocolMessageRole.Request,
            ProtocolMessageType.ReadPropertyRequest,
            new CorrelationId(1),
            ReadOnlyMemory<byte>.Empty);

        Assert.Throws<NotSupportedException>(
            () => codec.Decode(envelope));
    }

    [Fact]
    public void Decode_DiscoverRequestWithWrongRole_ThrowsInvalidDataException()
    {
        BinaryProtocolPayloadCodec codec = new();

        ProtocolEnvelope envelope = new(
            ProtocolVersion.Current,
            ProtocolMessageRole.Response,
            ProtocolMessageType.DiscoverRequest,
            new CorrelationId(1),
            ReadOnlyMemory<byte>.Empty);

        Assert.Throws<InvalidDataException>(
            () => codec.Decode(envelope));
    }

    [Fact]
    public void Decode_DiscoverRequestWithPayload_ThrowsInvalidDataException()
    {
        BinaryProtocolPayloadCodec codec = new();

        ProtocolEnvelope envelope = new(
            ProtocolVersion.Current,
            ProtocolMessageRole.Request,
            ProtocolMessageType.DiscoverRequest,
            new CorrelationId(1),
            new byte[] { 1 });

        Assert.Throws<InvalidDataException>(
            () => codec.Decode(envelope));
    }

    [Fact]
    public void Decode_UnsupportedProtocolVersion_ThrowsInvalidDataException()
    {
        BinaryProtocolPayloadCodec codec = new();

        ProtocolEnvelope envelope = new(
            new ProtocolVersion(2, 0),
            ProtocolMessageRole.Request,
            ProtocolMessageType.DiscoverRequest,
            new CorrelationId(1),
            ReadOnlyMemory<byte>.Empty);

        Assert.Throws<InvalidDataException>(
            () => codec.Decode(envelope));
    }
}