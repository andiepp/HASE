using Google.Protobuf.WellKnownTypes;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostAttachmentObservationPayloadMapperTests
{
    [Fact]
    public void Constructor_NullEndpointSnapshotMapper_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "endpointSnapshotMapper",
            () =>
                new RuntimeHostAttachmentObservationPayloadMapper(
                    null!));
    }

    [Fact]
    public void Map_NullPublishedPayload_ShouldThrow()
    {
        var mapper =
            CreateMapper();

        Assert.Throws<ArgumentNullException>(
            "payload",
            () =>
                mapper.Map(
                    (Northbound.RuntimeHostAttachmentPublishedObservationPayload)
                        null!));
    }

    [Fact]
    public void Map_NullEndedPayload_ShouldThrow()
    {
        var mapper =
            CreateMapper();

        Assert.Throws<ArgumentNullException>(
            "payload",
            () =>
                mapper.Map(
                    (Northbound.RuntimeHostAttachmentEndedObservationPayload)
                        null!));
    }

    [Fact]
    public void Map_PublishedPayload_ShouldMapCompleteEndpointSnapshot()
    {
        Northbound.PublishedRuntimeEndpointSnapshot endpoint =
            CreateEndpoint();
        var mappedEndpoint =
            new GrpcV1.PublishedRuntimeEndpointSnapshot
            {
                EndpointId =
                    endpoint.EndpointId.Value
            };
        var endpointMapper =
            new TestEndpointSnapshotMapper(
                mappedEndpoint);
        var mapper =
            new RuntimeHostAttachmentObservationPayloadMapper(
                endpointMapper);
        var payload =
            new Northbound.RuntimeHostAttachmentPublishedObservationPayload(
                endpoint);

        GrpcV1.AttachmentPublishedObservation result =
            mapper.Map(
                payload);

        Assert.Same(
            endpoint,
            endpointMapper.Input);
        Assert.Same(
            mappedEndpoint,
            result.Endpoint);
    }

    [Fact]
    public void Map_PublishedPayloadMapperReturnsNull_ShouldThrow()
    {
        var mapper =
            new RuntimeHostAttachmentObservationPayloadMapper(
                new TestEndpointSnapshotMapper(
                    null!));

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    mapper.Map(
                        new Northbound
                            .RuntimeHostAttachmentPublishedObservationPayload(
                                CreateEndpoint())));

        Assert.Equal(
            "The endpoint snapshot mapper returned null.",
            exception.Message);
    }

    [Fact]
    public void Map_EndedPayload_ShouldPreserveUtcTimestamp()
    {
        var endedAtUtc =
            new DateTimeOffset(
                2026,
                7,
                25,
                17,
                42,
                30,
                TimeSpan.Zero);
        var mapper =
            CreateMapper();

        GrpcV1.AttachmentEndedObservation result =
            mapper.Map(
                new Northbound.RuntimeHostAttachmentEndedObservationPayload(
                    endedAtUtc));

        Assert.Equal(
            Timestamp.FromDateTimeOffset(
                endedAtUtc),
            result.EndedAtUtc);
    }

    private static RuntimeHostAttachmentObservationPayloadMapper CreateMapper()
    {
        return new RuntimeHostAttachmentObservationPayloadMapper(
            new TestEndpointSnapshotMapper(
                new GrpcV1.PublishedRuntimeEndpointSnapshot()));
    }

    private static Northbound.PublishedRuntimeEndpointSnapshot CreateEndpoint()
    {
        return new Northbound.PublishedRuntimeEndpointSnapshot(
            new Northbound.RuntimeEndpointAttachmentGeneration(
                new Guid(
                    "868e79d4-b1a4-4a63-81cd-5a800d9ba3fd")),
            new EndpointDescriptor(
                new EndpointId(
                    "endpoint-01")),
            new EndpointConnectionStatus(
                EndpointConnectionState.Ready));
    }

    private sealed class TestEndpointSnapshotMapper
        : IRuntimeEndpointSnapshotMapper
    {
        private readonly GrpcV1.PublishedRuntimeEndpointSnapshot result;

        public TestEndpointSnapshotMapper(
            GrpcV1.PublishedRuntimeEndpointSnapshot result)
        {
            this.result =
                result;
        }

        public Northbound.PublishedRuntimeEndpointSnapshot? Input
        {
            get;
            private set;
        }

        public GrpcV1.PublishedRuntimeEndpointSnapshot Map(
            Northbound.PublishedRuntimeEndpointSnapshot snapshot)
        {
            Input =
                snapshot;

            return result;
        }
    }
}
