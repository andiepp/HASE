using Hase.Client;
using Hase.Client.Grpc;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using GrpcV1 = Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Client.Grpc.Tests;

public sealed class RuntimeHostGrpcCommandMapperTests
{
    [Fact]
    public void MapRequest_ParameterlessCommand_ShouldPreserveExactTarget()
    {
        var request =
            new RemoteCommandExecutionRequest(
                new RemoteCommandTarget(
                    new RemoteEndpointAttachmentKey(
                        new EndpointId(
                            "endpoint-01"),
                        new RemoteEndpointAttachmentGeneration(
                            Guid.Parse(
                                "9206a38d-d980-4495-bc11-f7cdf9f14ebd"))),
                    new InstrumentId(
                        "controller-01"),
                    DescriptorPath.Parse(
                        "Controller.ToggleLed")));

        GrpcV1.ExecuteCommandRequest result =
            new RuntimeHostGrpcCommandMapper().MapRequest(
                request);

        Assert.Equal(
            "endpoint-01",
            result.Target.EndpointId);
        Assert.Equal(
            "controller-01",
            result.Target.InstrumentId);
        Assert.Equal(
            ["Controller", "ToggleLed"],
            result.Target.CommandPathSegments);
        Assert.Null(
            result.Argument);
    }

    [Fact]
    public void MapResult_Failure_ShouldPreserveStatusAndDiagnostic()
    {
        var source =
            new GrpcV1.CommandOperationResult
            {
                Status =
                    GrpcV1.CommandOperationStatus.EndpointRejected,
                Diagnostic =
                    " Rejected. "
            };

        RemoteCommandOperationResult result =
            new RuntimeHostGrpcCommandMapper().MapResult(
                source);

        Assert.False(
            result.IsSuccess);
        Assert.Equal(
            RemoteCommandOperationStatus.EndpointRejected,
            result.Status);
        Assert.Equal(
            "Rejected.",
            result.Diagnostic);
    }
}
