using Hase.Core.Domain.Identity;
using Hase.Protocol;

namespace Hase.Protocol.Tests;

public sealed class ReadEndpointDescriptorRequestTests
{
    [Fact]
    public void Constructor_SetsProtocolProperties()
    {
        EndpointId endpointId = new("Endpoint-1");

        ReadEndpointDescriptorRequest request = new(
            new CorrelationId(17),
            endpointId);

        Assert.Equal(
            ProtocolVersion.Current,
            request.Version);

        Assert.Equal(
            ProtocolMessageRole.Request,
            request.Role);

        Assert.Equal(
            ProtocolMessageType.ReadEndpointDescriptorRequest,
            request.MessageType);

        Assert.Equal(
            new CorrelationId(17),
            request.CorrelationId);
    }

    [Fact]
    public void Constructor_StoresEndpointId()
    {
        EndpointId endpointId = new("Endpoint-1");

        ReadEndpointDescriptorRequest request = new(
            CorrelationId.None,
            endpointId);

        Assert.Equal(
            endpointId,
            request.EndpointId);
    }

    [Fact]
    public void RequestsWithSameValues_AreEqual()
    {
        EndpointId endpointId = new("Endpoint-1");

        ReadEndpointDescriptorRequest first = new(
            new CorrelationId(5),
            endpointId);

        ReadEndpointDescriptorRequest second = new(
            new CorrelationId(5),
            endpointId);

        Assert.Equal(first, second);
    }
}