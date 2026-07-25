using Hase.Core.Domain.Data;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Connections;
using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostPropertyMapperFactoryTests
{
    [Fact]
    public void Create_ComposedMappers_ShouldMapCompletePropertySurface()
    {
        RuntimeHostPropertyMappers mappers =
            RuntimeHostPropertyMapperFactory.Create();
        var generation =
            new Guid(
                "868e79d4-b1a4-4a63-81cd-5a800d9ba3fd");
        var remoteTarget =
            new GrpcV1.PropertyTarget
            {
                EndpointId =
                    "endpoint-01",
                AttachmentGeneration =
                    generation.ToString(
                        "D"),
                InstrumentId =
                    "environment-sensor-01",
                PropertyId =
                    "temperature"
            };

        Northbound.RuntimeHostPropertyTarget target =
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
            remoteTarget.PropertyId,
            target.PropertyId.Value);
        Assert.Equal(
            23.75,
            mappers.RemoteValueMapper.MapToClr(
                new GrpcV1.RemoteValue
                {
                    NumericValue =
                        23.75
                }));

        var descriptor =
            new PropertyDescriptor(
                target.PropertyId,
                new DescriptorPath(
                    "physical",
                    "environment-sensor",
                    "temperature"),
                "Temperature",
                new NumericDataDescriptor(
                    Quantities.Temperature,
                    Units.Celsius,
                    new ValueRange(
                        -40.0,
                        85.0),
                    new Resolution(
                        0.01)))
            {
                Description =
                    "Measured temperature.",
                AccessMode =
                    PropertyAccessMode.Read
            };
        var currentValue =
            new PropertyValue(
                23.75,
                DateTimeOffset.UnixEpoch,
                PropertyQuality.Good);
        var snapshot =
            new Northbound.PublishedRuntimePropertySnapshot(
                target,
                descriptor,
                new EndpointConnectionStatus(
                    EndpointConnectionState.Ready),
                currentValue);

        GrpcV1.CachedPropertyResult cachedResult =
            mappers.CachedResultMapper.Map(
                Northbound.RuntimeHostCachedPropertyResult.Successful(
                    snapshot));

        Assert.Equal(
            GrpcV1.PropertyOperationStatus.Success,
            cachedResult.Status);
        Assert.Equal(
            "endpoint-01",
            cachedResult.Snapshot.Target.EndpointId);
        Assert.Equal(
            "temperature",
            cachedResult.Snapshot.Descriptor_.PropertyId);
        Assert.Equal(
            GrpcV1.DataDescriptor.KindOneofCase.Numeric,
            cachedResult.Snapshot.Descriptor_.Data.KindCase);
        Assert.Equal(
            "celsius",
            cachedResult.Snapshot.Descriptor_.Data.Numeric.NativeUnit.Id);
        Assert.Equal(
            23.75,
            cachedResult.Snapshot.CurrentValue.Value.NumericValue);
        Assert.Equal(
            GrpcV1.PropertyQuality.Good,
            cachedResult.Snapshot.CurrentValue.Quality);

        GrpcV1.PropertyOperationResult operationResult =
            mappers.OperationResultMapper.Map(
                Northbound.RuntimeHostPropertyOperationResult.Failed(
                    Northbound.RuntimeHostPropertyOperationStatus.TimedOut,
                    "Endpoint operation timed out."));

        Assert.Equal(
            GrpcV1.PropertyOperationStatus.TimedOut,
            operationResult.Status);
        Assert.Null(
            operationResult.ConfirmedValue);
        Assert.Equal(
            "Endpoint operation timed out.",
            operationResult.Diagnostic);
    }
}
