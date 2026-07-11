using Hase.Protocol;

namespace Hase.Protocol.Tests;

public sealed class ProtocolMessageTests
{
    private sealed record TestMessage(
        ProtocolVersion Version,
        ProtocolMessageRole Role,
        ProtocolMessageType MessageType,
        CorrelationId CorrelationId)
        : ProtocolMessage(
            Version,
            Role,
            MessageType,
            CorrelationId);

    [Fact]
    public void Constructor_SetsProperties()
    {
        TestMessage message = new(
            ProtocolVersion.Current,
            ProtocolMessageRole.Request,
            ProtocolMessageType.DiscoverRequest,
            new CorrelationId(42));

        Assert.Equal(ProtocolVersion.Current, message.Version);
        Assert.Equal(ProtocolMessageRole.Request, message.Role);
        Assert.Equal(ProtocolMessageType.DiscoverRequest, message.MessageType);
        Assert.Equal(new CorrelationId(42), message.CorrelationId);
    }

    [Fact]
    public void RecordEquality_Works()
    {
        TestMessage first = new(
            ProtocolVersion.Current,
            ProtocolMessageRole.Request,
            ProtocolMessageType.DiscoverRequest,
            new CorrelationId(7));

        TestMessage second = new(
            ProtocolVersion.Current,
            ProtocolMessageRole.Request,
            ProtocolMessageType.DiscoverRequest,
            new CorrelationId(7));

        Assert.Equal(first, second);
    }

    [Fact]
    public void DifferentCorrelationIds_AreNotEqual()
    {
        TestMessage first = new(
            ProtocolVersion.Current,
            ProtocolMessageRole.Request,
            ProtocolMessageType.DiscoverRequest,
            new CorrelationId(1));

        TestMessage second = new(
            ProtocolVersion.Current,
            ProtocolMessageRole.Request,
            ProtocolMessageType.DiscoverRequest,
            new CorrelationId(2));

        Assert.NotEqual(first, second);
    }
}