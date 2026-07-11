using Hase.Protocol;

namespace Hase.Protocol.Tests;

public sealed class ProtocolResponseTests
{
    private sealed record TestResponse(
        ProtocolVersion Version,
        ProtocolMessageType MessageType,
        CorrelationId CorrelationId)
        : ProtocolResponse(
            Version,
            MessageType,
            CorrelationId);

    [Fact]
    public void Constructor_SetsResponseRole()
    {
        TestResponse response = new(
            ProtocolVersion.Current,
            ProtocolMessageType.DiscoverResponse,
            new CorrelationId(42));

        Assert.Equal(
            ProtocolMessageRole.Response,
            response.Role);
    }

    [Fact]
    public void Constructor_SetsProtocolProperties()
    {
        TestResponse response = new(
            new ProtocolVersion(2, 3),
            ProtocolMessageType.DiscoverResponse,
            new CorrelationId(17));

        Assert.Equal(
            new ProtocolVersion(2, 3),
            response.Version);

        Assert.Equal(
            ProtocolMessageType.DiscoverResponse,
            response.MessageType);

        Assert.Equal(
            new CorrelationId(17),
            response.CorrelationId);
    }

    [Fact]
    public void ResponsesWithSameValues_AreEqual()
    {
        TestResponse first = new(
            ProtocolVersion.Current,
            ProtocolMessageType.DiscoverResponse,
            new CorrelationId(7));

        TestResponse second = new(
            ProtocolVersion.Current,
            ProtocolMessageType.DiscoverResponse,
            new CorrelationId(7));

        Assert.Equal(first, second);
    }
}