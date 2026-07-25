using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeEndpointSnapshotMapperTests
{
    [Fact]
    public void Constructor_NullDescriptorMapper_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "descriptorMapper",
            () =>
                new RuntimeEndpointSnapshotMapper(
                    null!,
                    new TestConnectionStatusMapper(
                        new GrpcV1.EndpointConnectionStatus())));
    }

    [Fact]
    public void Constructor_NullConnectionStatusMapper_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "connectionStatusMapper",
            () =>
                new RuntimeEndpointSnapshotMapper(
                    new TestDescriptorMapper(
                        new GrpcV1.EndpointDescriptor()),
                    null!));
    }

    [Fact]
    public void Map_NullSnapshot_ShouldThrow()
    {
        var mapper =
            new RuntimeEndpointSnapshotMapper(
                new TestDescriptorMapper(
                    new GrpcV1.EndpointDescriptor()),
                new TestConnectionStatusMapper(
                    new GrpcV1.EndpointConnectionStatus()));

        Assert.Throws<ArgumentNullException>(
            "snapshot",
            () =>
                mapper.Map(
                    null!));
    }

    [Fact]
    public void Map_Snapshot_ShouldMapEnvelopeAndDelegateChildren()
    {
        var generation =
            new Northbound.RuntimeEndpointAttachmentGeneration(
                Guid.Parse(
                    "0a11d9d4-7a02-43be-ae3f-eef9d11e0de8"));
        var descriptor =
            new EndpointDescriptor(
                new EndpointId(
                    "endpoint-1"));
        var connectionStatus =
            new EndpointConnectionStatus(
                EndpointConnectionState.Ready);

        var mappedDescriptor =
            new GrpcV1.EndpointDescriptor
            {
                EndpointId =
                    "mapped-endpoint-1"
            };
        var mappedConnectionStatus =
            new GrpcV1.EndpointConnectionStatus
            {
                Detail =
                    "mapped-ready"
            };

        var descriptorMapper =
            new TestDescriptorMapper(
                mappedDescriptor);
        var connectionStatusMapper =
            new TestConnectionStatusMapper(
                mappedConnectionStatus);

        var mapper =
            new RuntimeEndpointSnapshotMapper(
                descriptorMapper,
                connectionStatusMapper);

        GrpcV1.PublishedRuntimeEndpointSnapshot result =
            mapper.Map(
                new Northbound.PublishedRuntimeEndpointSnapshot(
                    generation,
                    descriptor,
                    connectionStatus));

        Assert.Equal(
            "endpoint-1",
            result.EndpointId);
        Assert.Equal(
            "0a11d9d4-7a02-43be-ae3f-eef9d11e0de8",
            result.AttachmentGeneration);
        Assert.Same(
            mappedDescriptor,
            result.Descriptor_);
        Assert.Same(
            mappedConnectionStatus,
            result.ConnectionStatus);
        Assert.Same(
            descriptor,
            descriptorMapper.Input);
        Assert.Same(
            connectionStatus,
            connectionStatusMapper.Input);
    }

    [Fact]
    public void Map_DescriptorMapperReturnsNull_ShouldThrow()
    {
        var mapper =
            new RuntimeEndpointSnapshotMapper(
                new TestDescriptorMapper(
                    null!),
                new TestConnectionStatusMapper(
                    new GrpcV1.EndpointConnectionStatus()));

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    mapper.Map(
                        CreateSnapshot()));

        Assert.Equal(
            "The endpoint descriptor mapper returned null.",
            exception.Message);
    }

    [Fact]
    public void Map_ConnectionStatusMapperReturnsNull_ShouldThrow()
    {
        var mapper =
            new RuntimeEndpointSnapshotMapper(
                new TestDescriptorMapper(
                    new GrpcV1.EndpointDescriptor()),
                new TestConnectionStatusMapper(
                    null!));

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    mapper.Map(
                        CreateSnapshot()));

        Assert.Equal(
            "The endpoint connection status mapper returned null.",
            exception.Message);
    }

    private static Northbound.PublishedRuntimeEndpointSnapshot CreateSnapshot()
    {
        return new Northbound.PublishedRuntimeEndpointSnapshot(
            Northbound.RuntimeEndpointAttachmentGeneration.CreateNew(),
            new EndpointDescriptor(
                new EndpointId(
                    "endpoint-1")),
            new EndpointConnectionStatus(
                EndpointConnectionState.Ready));
    }

    private sealed class TestDescriptorMapper
        : IEndpointDescriptorMapper
    {
        private readonly GrpcV1.EndpointDescriptor result;

        public TestDescriptorMapper(
            GrpcV1.EndpointDescriptor result)
        {
            this.result =
                result;
        }

        public EndpointDescriptor? Input
        {
            get;
            private set;
        }

        public GrpcV1.EndpointDescriptor Map(
            EndpointDescriptor descriptor)
        {
            Input =
                descriptor;

            return result;
        }
    }

    private sealed class TestConnectionStatusMapper
        : IEndpointConnectionStatusMapper
    {
        private readonly GrpcV1.EndpointConnectionStatus result;

        public TestConnectionStatusMapper(
            GrpcV1.EndpointConnectionStatus result)
        {
            this.result =
                result;
        }

        public EndpointConnectionStatus? Input
        {
            get;
            private set;
        }

        public GrpcV1.EndpointConnectionStatus Map(
            EndpointConnectionStatus status)
        {
            Input =
                status;

            return result;
        }
    }
}
