using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostCommandMapperFactoryTests
{
    [Fact]
    public void Create_ComposedMappers_ShouldMapCompleteCommandSurface()
    {
        RuntimeHostCommandMappers mappers =
            RuntimeHostCommandMapperFactory.Create();
        var generation =
            new Guid(
                "868e79d4-b1a4-4a63-81cd-5a800d9ba3fd");
        var remoteTarget =
            new GrpcV1.CommandTarget
            {
                EndpointId =
                    "endpoint-01",
                AttachmentGeneration =
                    generation.ToString(
                        "D"),
                InstrumentId =
                    "environment-sensor-01"
            };

        remoteTarget.CommandPathSegments.Add(
            "Calibration");
        remoteTarget.CommandPathSegments.Add(
            "Reset");

        Northbound.RuntimeHostCommandTarget target =
            mappers.TargetMapper.Map(
                remoteTarget);

        Assert.Equal(
            remoteTarget.EndpointId,
            target.EndpointId.Value);
        Assert.Equal(
            generation,
            target.AttachmentGeneration.Value);
        Assert.Equal(
            remoteTarget.InstrumentId,
            target.InstrumentId.Value);
        Assert.Equal(
            remoteTarget.CommandPathSegments,
            target.CommandPath.Segments);
        Assert.Equal(
            true,
            mappers.RemoteValueMapper.MapToClr(
                new GrpcV1.RemoteValue
                {
                    BooleanValue =
                        true
                }));

        GrpcV1.CommandOperationResult operationResult =
            mappers.OperationResultMapper.Map(
                Northbound.RuntimeHostCommandOperationResult.Failed(
                    Northbound.RuntimeHostCommandOperationStatus.TimedOut,
                    "Endpoint operation timed out."));

        Assert.Equal(
            GrpcV1.CommandOperationStatus.TimedOut,
            operationResult.Status);
        Assert.Null(
            operationResult.ReturnValue);
        Assert.Equal(
            "Endpoint operation timed out.",
            operationResult.Diagnostic);
    }
}
