using Hase.Protocol;

namespace Hase.Protocol.Tests;

public sealed class ProtocolEnvelopeTests
{
    [Fact]
    public void Constructor_StoresHeaderInformation()
    {
        ProtocolEnvelope envelope = new(
            ProtocolVersion.Current,
            ProtocolMessageRole.Request,
            ProtocolMessageType.ReadPropertyRequest,
            new CorrelationId(17),
            ReadOnlyMemory<byte>.Empty);

        Assert.Equal(
            ProtocolVersion.Current,
            envelope.Version);

        Assert.Equal(
            ProtocolMessageRole.Request,
            envelope.Role);

        Assert.Equal(
            ProtocolMessageType.ReadPropertyRequest,
            envelope.MessageType);

        Assert.Equal(
            new CorrelationId(17),
            envelope.CorrelationId);
    }

    [Fact]
    public void Constructor_StoresPayload()
    {
        byte[] payload = [10, 20, 30];

        ProtocolEnvelope envelope = new(
            ProtocolVersion.Current,
            ProtocolMessageRole.Response,
            ProtocolMessageType.ReadPropertyResponse,
            new CorrelationId(5),
            payload);

        Assert.True(
            payload.AsSpan().SequenceEqual(
                envelope.Payload.Span));
    }

    [Fact]
    public void PayloadLength_ReturnsActualPayloadLength()
    {
        ProtocolEnvelope envelope = new(
            ProtocolVersion.Current,
            ProtocolMessageRole.Notification,
            ProtocolMessageType.EventNotification,
            CorrelationId.None,
            new byte[] { 1, 2, 3, 4 });

        Assert.Equal(
            4,
            envelope.PayloadLength);
    }

    [Fact]
    public void EmptyPayload_HasLengthZero()
    {
        ProtocolEnvelope envelope = new(
            ProtocolVersion.Current,
            ProtocolMessageRole.Request,
            ProtocolMessageType.DiscoverRequest,
            new CorrelationId(1),
            ReadOnlyMemory<byte>.Empty);

        Assert.Equal(
            0,
            envelope.PayloadLength);

        Assert.True(
            envelope.Payload.IsEmpty);
    }
}