using Hase.Core.Domain.Data;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Connections;
using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostCachedPropertyResultMapperTests
{
    [Fact]
    public void Constructor_NullStatusMapper_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "statusMapper",
            () =>
                new RuntimeHostCachedPropertyResultMapper(
                    null!,
                    CreateSnapshotMapper()));
    }

    [Fact]
    public void Constructor_NullSnapshotMapper_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "snapshotMapper",
            () =>
                new RuntimeHostCachedPropertyResultMapper(
                    CreateStatusMapper(),
                    null!));
    }

    [Fact]
    public void Map_NullResult_ShouldThrow()
    {
        var mapper =
            CreateMapper();

        Assert.Throws<ArgumentNullException>(
            "result",
            () =>
                mapper.Map(
                    null!));
    }

    [Fact]
    public void Map_Success_ShouldMapStatusAndSnapshot()
    {
        Northbound.PublishedRuntimePropertySnapshot snapshot =
            CreateSnapshot();
        var mappedSnapshot =
            new GrpcV1.PublishedRuntimePropertySnapshot();
        var statusMapper =
            CreateStatusMapper();
        var snapshotMapper =
            new TestSnapshotMapper(
                mappedSnapshot);
        var mapper =
            new RuntimeHostCachedPropertyResultMapper(
                statusMapper,
                snapshotMapper);

        GrpcV1.CachedPropertyResult result =
            mapper.Map(
                Northbound.RuntimeHostCachedPropertyResult.Successful(
                    snapshot));

        Assert.Equal(
            Northbound.RuntimeHostPropertyOperationStatus.Success,
            statusMapper.Input);
        Assert.Equal(
            GrpcV1.PropertyOperationStatus.Success,
            result.Status);
        Assert.Same(
            snapshot,
            snapshotMapper.Input);
        Assert.Same(
            mappedSnapshot,
            result.Snapshot);
        Assert.False(
            result.HasDiagnostic);
    }

    [Fact]
    public void Map_Failure_ShouldMapStatusDiagnosticAndSnapshotAbsence()
    {
        var statusMapper =
            new TestStatusMapper(
                GrpcV1.PropertyOperationStatus.EndpointUnavailable);
        var snapshotMapper =
            CreateSnapshotMapper();
        var mapper =
            new RuntimeHostCachedPropertyResultMapper(
                statusMapper,
                snapshotMapper);

        GrpcV1.CachedPropertyResult result =
            mapper.Map(
                Northbound.RuntimeHostCachedPropertyResult.Failed(
                    Northbound.RuntimeHostPropertyOperationStatus.EndpointUnavailable,
                    "Endpoint is not Ready."));

        Assert.Equal(
            Northbound.RuntimeHostPropertyOperationStatus.EndpointUnavailable,
            statusMapper.Input);
        Assert.Equal(
            GrpcV1.PropertyOperationStatus.EndpointUnavailable,
            result.Status);
        Assert.Null(
            snapshotMapper.Input);
        Assert.Null(
            result.Snapshot);
        Assert.True(
            result.HasDiagnostic);
        Assert.Equal(
            "Endpoint is not Ready.",
            result.Diagnostic);
    }

    [Fact]
    public void Map_SnapshotMapperReturnsNull_ShouldThrow()
    {
        var mapper =
            new RuntimeHostCachedPropertyResultMapper(
                CreateStatusMapper(),
                new TestSnapshotMapper(
                    null!));

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    mapper.Map(
                        Northbound.RuntimeHostCachedPropertyResult.Successful(
                            CreateSnapshot())));

        Assert.Equal(
            "The published Property snapshot mapper returned null.",
            exception.Message);
    }

    private static RuntimeHostCachedPropertyResultMapper CreateMapper()
    {
        return new RuntimeHostCachedPropertyResultMapper(
            CreateStatusMapper(),
            CreateSnapshotMapper());
    }

    private static TestStatusMapper CreateStatusMapper()
    {
        return new TestStatusMapper(
            GrpcV1.PropertyOperationStatus.Success);
    }

    private static TestSnapshotMapper CreateSnapshotMapper()
    {
        return new TestSnapshotMapper(
            new GrpcV1.PublishedRuntimePropertySnapshot());
    }

    private static Northbound.PublishedRuntimePropertySnapshot CreateSnapshot()
    {
        var propertyId =
            new PropertyId(
                "temperature");
        var target =
            new Northbound.RuntimeHostPropertyTarget(
                new EndpointId(
                    "endpoint-01"),
                new Northbound.RuntimeEndpointAttachmentGeneration(
                    new Guid(
                        "868e79d4-b1a4-4a63-81cd-5a800d9ba3fd")),
                new InstrumentId(
                    "environment-sensor-01"),
                propertyId);
        var descriptor =
            new PropertyDescriptor(
                propertyId,
                new DescriptorPath(
                    "physical",
                    "environment-sensor",
                    "temperature"),
                "Temperature",
                new StringDataDescriptor());

        return new Northbound.PublishedRuntimePropertySnapshot(
            target,
            descriptor,
            new EndpointConnectionStatus(
                EndpointConnectionState.Ready),
            currentValue: null);
    }

    private sealed class TestStatusMapper
        : IRuntimeHostPropertyOperationStatusMapper
    {
        private readonly GrpcV1.PropertyOperationStatus result;

        public TestStatusMapper(
            GrpcV1.PropertyOperationStatus result)
        {
            this.result =
                result;
        }

        public Northbound.RuntimeHostPropertyOperationStatus? Input
        {
            get;
            private set;
        }

        public GrpcV1.PropertyOperationStatus Map(
            Northbound.RuntimeHostPropertyOperationStatus status)
        {
            Input =
                status;

            return result;
        }
    }

    private sealed class TestSnapshotMapper
        : IPublishedRuntimePropertySnapshotMapper
    {
        private readonly GrpcV1.PublishedRuntimePropertySnapshot result;

        public TestSnapshotMapper(
            GrpcV1.PublishedRuntimePropertySnapshot result)
        {
            this.result =
                result;
        }

        public Northbound.PublishedRuntimePropertySnapshot? Input
        {
            get;
            private set;
        }

        public GrpcV1.PublishedRuntimePropertySnapshot Map(
            Northbound.PublishedRuntimePropertySnapshot snapshot)
        {
            Input =
                snapshot;

            return result;
        }
    }
}
