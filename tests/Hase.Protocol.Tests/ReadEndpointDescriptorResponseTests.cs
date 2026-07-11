using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Protocol;

namespace Hase.Protocol.Tests;

public sealed class ReadEndpointDescriptorResponseTests
{
    [Fact]
    public void SuccessfulResponse_SetsProtocolProperties()
    {
        EndpointDescriptor descriptor =
            new(new EndpointId("Endpoint-1"));

        ReadEndpointDescriptorResponse response =
            new(
                new CorrelationId(17),
                ProtocolResult.Success,
                descriptor);

        Assert.Equal(
            ProtocolVersion.Current,
            response.Version);

        Assert.Equal(
            ProtocolMessageRole.Response,
            response.Role);

        Assert.Equal(
            ProtocolMessageType.ReadEndpointDescriptorResponse,
            response.MessageType);

        Assert.Equal(
            new CorrelationId(17),
            response.CorrelationId);
    }

    [Fact]
    public void SuccessfulResponse_StoresResult()
    {
        EndpointDescriptor descriptor =
            new(new EndpointId("Endpoint-1"));

        ReadEndpointDescriptorResponse response =
            new(
                CorrelationId.None,
                ProtocolResult.Success,
                descriptor);

        Assert.Equal(
            ProtocolResult.Success,
            response.Result);
    }

    [Fact]
    public void SuccessfulResponse_StoresDescriptor()
    {
        EndpointDescriptor descriptor =
            new(new EndpointId("Endpoint-1"));

        ReadEndpointDescriptorResponse response =
            new(
                CorrelationId.None,
                ProtocolResult.Success,
                descriptor);

        Assert.Same(
            descriptor,
            response.Descriptor);
    }

    [Fact]
    public void FailedResponse_CanOmitDescriptor()
    {
        ProtocolResult result =
            new(
                ProtocolResultCode.NotFound,
                "Endpoint not found.");

        ReadEndpointDescriptorResponse response =
            new(
                new CorrelationId(5),
                result,
                null);

        Assert.Equal(result, response.Result);
        Assert.Null(response.Descriptor);
    }
}