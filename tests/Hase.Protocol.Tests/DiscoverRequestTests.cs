using Hase.Protocol;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;

namespace Hase.Protocol.Tests;

public sealed class DiscoverRequestTests
{
    [Fact]
    public void Constructor_SetsDefaultProperties()
    {
        DiscoverRequest request = new(new CorrelationId(17));

        Assert.Equal(
            ProtocolVersion.Current,
            request.Version);

        Assert.Equal(
            ProtocolMessageRole.Request,
            request.Role);

        Assert.Equal(
            ProtocolMessageType.DiscoverRequest,
            request.MessageType);

        Assert.Equal(
            new CorrelationId(17),
            request.CorrelationId);
    }

    [Fact]
    public void TwoRequestsWithSameCorrelationId_AreEqual()
    {
        Assert.Equal(
            new DiscoverRequest(new CorrelationId(5)),
            new DiscoverRequest(new CorrelationId(5)));
    }

    [Fact]
    public void DifferentCorrelationIds_AreNotEqual()
    {
        Assert.NotEqual(
            new DiscoverRequest(new CorrelationId(1)),
            new DiscoverRequest(new CorrelationId(2)));
    }
}