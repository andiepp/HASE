using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostSnapshotMapperTests
{
    [Fact]
    public void Constructor_NullEndpointSnapshotMapper_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "endpointSnapshotMapper",
            () =>
                new RuntimeHostSnapshotMapper(
                    null!));
    }

    [Fact]
    public void Map_NullSnapshot_ShouldThrow()
    {
        var mapper =
            new RuntimeHostSnapshotMapper(
                new TestEndpointSnapshotMapper());

        Assert.Throws<ArgumentNullException>(
            "snapshot",
            () =>
                mapper.Map(
                    null!));
    }

    [Fact]
    public void Map_EmptySnapshot_ShouldMapHostIdentityAndApiVersion()
    {
        var endpointMapper =
            new TestEndpointSnapshotMapper();

        var mapper =
            new RuntimeHostSnapshotMapper(
                endpointMapper);

        GrpcV1.GetSnapshotResponse response =
            mapper.Map(
                new Northbound.PublishedRuntimeHostSnapshot(
                    new Northbound.RuntimeHostId(
                        "runtime-host-1"),
                    new Northbound.RuntimeHostApiVersion(
                        1,
                        7),
                    Array.Empty<Northbound.PublishedRuntimeEndpointSnapshot>()));

        Assert.Equal(
            "runtime-host-1",
            response.RuntimeHostId);
        Assert.Equal(
            1U,
            response.ApiVersion.Major);
        Assert.Equal(
            7U,
            response.ApiVersion.Minor);
        Assert.Empty(
            response.Endpoints);
        Assert.Empty(
            endpointMapper.Inputs);
    }

    [Fact]
    public void Map_EndpointSnapshots_ShouldDelegateInOrderAndPreserveResults()
    {
        Northbound.PublishedRuntimeEndpointSnapshot firstInput =
            CreateEndpointSnapshot(
                "endpoint-1");
        Northbound.PublishedRuntimeEndpointSnapshot secondInput =
            CreateEndpointSnapshot(
                "endpoint-2");

        var firstOutput =
            new GrpcV1.PublishedRuntimeEndpointSnapshot
            {
                EndpointId =
                    "mapped-endpoint-1"
            };
        var secondOutput =
            new GrpcV1.PublishedRuntimeEndpointSnapshot
            {
                EndpointId =
                    "mapped-endpoint-2"
            };

        var endpointMapper =
            new TestEndpointSnapshotMapper(
                firstOutput,
                secondOutput);

        var mapper =
            new RuntimeHostSnapshotMapper(
                endpointMapper);

        GrpcV1.GetSnapshotResponse response =
            mapper.Map(
                new Northbound.PublishedRuntimeHostSnapshot(
                    new Northbound.RuntimeHostId(
                        "runtime-host-1"),
                    Northbound.RuntimeHostApiVersion.Current,
                    new[]
                    {
                        firstInput,
                        secondInput
                    }));

        Assert.Equal(
            new[]
            {
                firstInput,
                secondInput
            },
            endpointMapper.Inputs.ToArray());
        Assert.Equal(
            new[]
            {
                firstOutput,
                secondOutput
            },
            response.Endpoints.ToArray());
    }

    [Fact]
    public void Map_EndpointMapperReturnsNull_ShouldThrow()
    {
        var mapper =
            new RuntimeHostSnapshotMapper(
                new TestEndpointSnapshotMapper(
                    new GrpcV1.PublishedRuntimeEndpointSnapshot[]
                    {
                        null!
                    }));

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    mapper.Map(
                        new Northbound.PublishedRuntimeHostSnapshot(
                            new Northbound.RuntimeHostId(
                                "runtime-host-1"),
                            Northbound.RuntimeHostApiVersion.Current,
                            new[]
                            {
                                CreateEndpointSnapshot(
                                    "endpoint-1")
                            })));

        Assert.Equal(
            "The endpoint snapshot mapper returned null.",
            exception.Message);
    }

    private static Northbound.PublishedRuntimeEndpointSnapshot
        CreateEndpointSnapshot(
            string endpointId)
    {
        return new Northbound.PublishedRuntimeEndpointSnapshot(
            Northbound.RuntimeEndpointAttachmentGeneration.CreateNew(),
            new EndpointDescriptor(
                new EndpointId(
                    endpointId)),
            new EndpointConnectionStatus(
                EndpointConnectionState.Ready));
    }

    private sealed class TestEndpointSnapshotMapper
        : IRuntimeEndpointSnapshotMapper
    {
        private readonly Queue<GrpcV1.PublishedRuntimeEndpointSnapshot>
            outputs;

        public TestEndpointSnapshotMapper(
            params GrpcV1.PublishedRuntimeEndpointSnapshot[] outputs)
        {
            this.outputs =
                new Queue<GrpcV1.PublishedRuntimeEndpointSnapshot>(
                    outputs);
        }

        public List<Northbound.PublishedRuntimeEndpointSnapshot> Inputs
        {
            get;
        } =
            new();

        public GrpcV1.PublishedRuntimeEndpointSnapshot Map(
            Northbound.PublishedRuntimeEndpointSnapshot snapshot)
        {
            Inputs.Add(
                snapshot);

            return outputs.Dequeue();
        }
    }
}
