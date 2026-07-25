using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Connections;
using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostObservationMapperTests
{
    [Fact]
    public void Constructor_NullDependency_ShouldThrow()
    {
        MapperDependencies dependencies =
            CreateDependencies();

        Assert.Throws<ArgumentNullException>(
            "kindMapper",
            () =>
                new RuntimeHostObservationMapper(
                    null!,
                    dependencies.AttachmentMapper,
                    dependencies.ConnectionStatusMapper,
                    dependencies.PropertyValueMapper,
                    dependencies.EventMapper));
        Assert.Throws<ArgumentNullException>(
            "attachmentPayloadMapper",
            () =>
                new RuntimeHostObservationMapper(
                    dependencies.KindMapper,
                    null!,
                    dependencies.ConnectionStatusMapper,
                    dependencies.PropertyValueMapper,
                    dependencies.EventMapper));
        Assert.Throws<ArgumentNullException>(
            "connectionStatusPayloadMapper",
            () =>
                new RuntimeHostObservationMapper(
                    dependencies.KindMapper,
                    dependencies.AttachmentMapper,
                    null!,
                    dependencies.PropertyValueMapper,
                    dependencies.EventMapper));
        Assert.Throws<ArgumentNullException>(
            "propertyValuePayloadMapper",
            () =>
                new RuntimeHostObservationMapper(
                    dependencies.KindMapper,
                    dependencies.AttachmentMapper,
                    dependencies.ConnectionStatusMapper,
                    null!,
                    dependencies.EventMapper));
        Assert.Throws<ArgumentNullException>(
            "eventPayloadMapper",
            () =>
                new RuntimeHostObservationMapper(
                    dependencies.KindMapper,
                    dependencies.AttachmentMapper,
                    dependencies.ConnectionStatusMapper,
                    dependencies.PropertyValueMapper,
                    null!));
    }

    [Fact]
    public void Map_NullObservation_ShouldThrow()
    {
        RuntimeHostObservationMapper mapper =
            CreateMapper();

        Assert.Throws<ArgumentNullException>(
            "observation",
            () =>
                mapper.Map(
                    null!));
    }

    [Fact]
    public void Map_AttachmentPublished_ShouldMapEnvelopeAndPayload()
    {
        GrpcV1.ObserveResponse result =
            CreateMapper().Map(
                CreateObservation(
                    new Northbound
                        .RuntimeHostAttachmentPublishedObservationPayload(
                            CreateEndpointSnapshot())));

        AssertEnvelope(
            result,
            GrpcV1.RuntimeHostObservationKind.AttachmentPublished);
        Assert.Equal(
            GrpcV1.RuntimeHostObservation.PayloadOneofCase.AttachmentPublished,
            result.Observation.PayloadCase);
    }

    [Fact]
    public void Map_AttachmentEnded_ShouldMapEnvelopeAndPayload()
    {
        GrpcV1.ObserveResponse result =
            CreateMapper().Map(
                CreateObservation(
                    new Northbound
                        .RuntimeHostAttachmentEndedObservationPayload(
                            DateTimeOffset.UnixEpoch)));

        AssertEnvelope(
            result,
            GrpcV1.RuntimeHostObservationKind.AttachmentEnded);
        Assert.Equal(
            GrpcV1.RuntimeHostObservation.PayloadOneofCase.AttachmentEnded,
            result.Observation.PayloadCase);
    }

    [Fact]
    public void Map_ConnectionStatusChanged_ShouldMapEnvelopeAndPayload()
    {
        GrpcV1.ObserveResponse result =
            CreateMapper().Map(
                CreateObservation(
                    new Northbound
                        .RuntimeHostConnectionStatusChangedObservationPayload(
                            new EndpointConnectionStatus(
                                EndpointConnectionState.Connecting),
                            new EndpointConnectionStatus(
                                EndpointConnectionState.Ready))));

        AssertEnvelope(
            result,
            GrpcV1.RuntimeHostObservationKind.ConnectionStatusChanged);
        Assert.Equal(
            GrpcV1.RuntimeHostObservation.PayloadOneofCase
                .ConnectionStatusChanged,
            result.Observation.PayloadCase);
    }

    [Fact]
    public void Map_PropertyValueChanged_ShouldMapEnvelopeAndPayload()
    {
        GrpcV1.ObserveResponse result =
            CreateMapper().Map(
                CreateObservation(
                    new Northbound
                        .RuntimeHostPropertyValueChangedObservationPayload(
                            new InstrumentId(
                                "controller-01"),
                            new PropertyId(
                                "state"),
                            previousValue: null,
                            currentValue:
                                new PropertyValue(
                                    true,
                                    DateTimeOffset.UnixEpoch))));

        AssertEnvelope(
            result,
            GrpcV1.RuntimeHostObservationKind.PropertyValueChanged);
        Assert.Equal(
            GrpcV1.RuntimeHostObservation.PayloadOneofCase.PropertyValueChanged,
            result.Observation.PayloadCase);
    }

    [Fact]
    public void Map_EventOccurred_ShouldMapEnvelopeAndPayload()
    {
        GrpcV1.ObserveResponse result =
            CreateMapper().Map(
                CreateObservation(
                    new Northbound.RuntimeHostEventOccurredObservationPayload(
                        new InstrumentId(
                            "controller-01"),
                        new DescriptorPath(
                            "Controller",
                            "ButtonPressed"),
                        DateTimeOffset.UnixEpoch,
                        value: null)));

        AssertEnvelope(
            result,
            GrpcV1.RuntimeHostObservationKind.EventOccurred);
        Assert.Equal(
            GrpcV1.RuntimeHostObservation.PayloadOneofCase.EventOccurred,
            result.Observation.PayloadCase);
    }

    private static void AssertEnvelope(
        GrpcV1.ObserveResponse result,
        GrpcV1.RuntimeHostObservationKind expectedKind)
    {
        Assert.Equal(
            GrpcV1.ObserveResponse.ContentOneofCase.Observation,
            result.ContentCase);
        Assert.Equal(
            7UL,
            result.Observation.Sequence);
        Assert.Equal(
            "endpoint-01",
            result.Observation.EndpointId);
        Assert.Equal(
            "868e79d4-b1a4-4a63-81cd-5a800d9ba3fd",
            result.Observation.AttachmentGeneration);
        Assert.Equal(
            expectedKind,
            result.Observation.Kind);
    }

    private static RuntimeHostObservationMapper CreateMapper()
    {
        MapperDependencies dependencies =
            CreateDependencies();

        return new RuntimeHostObservationMapper(
            dependencies.KindMapper,
            dependencies.AttachmentMapper,
            dependencies.ConnectionStatusMapper,
            dependencies.PropertyValueMapper,
            dependencies.EventMapper);
    }

    private static MapperDependencies CreateDependencies()
    {
        return new MapperDependencies(
            new TestKindMapper(),
            new TestAttachmentMapper(),
            new TestConnectionStatusMapper(),
            new TestPropertyValueMapper(),
            new TestEventMapper());
    }

    private static Northbound.RuntimeHostObservation CreateObservation(
        Northbound.RuntimeHostObservationPayload payload)
    {
        return new Northbound.RuntimeHostObservation(
            new Northbound.RuntimeHostObservationSequence(
                7),
            new EndpointId(
                "endpoint-01"),
            new Northbound.RuntimeEndpointAttachmentGeneration(
                new Guid(
                    "868e79d4-b1a4-4a63-81cd-5a800d9ba3fd")),
            payload);
    }

    private static Northbound.PublishedRuntimeEndpointSnapshot
        CreateEndpointSnapshot()
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

    private sealed record MapperDependencies(
        IRuntimeHostObservationKindMapper KindMapper,
        IRuntimeHostAttachmentObservationPayloadMapper AttachmentMapper,
        IRuntimeHostConnectionStatusChangedObservationPayloadMapper
            ConnectionStatusMapper,
        IRuntimeHostPropertyValueChangedObservationPayloadMapper
            PropertyValueMapper,
        IRuntimeHostEventOccurredObservationPayloadMapper EventMapper);

    private sealed class TestKindMapper
        : IRuntimeHostObservationKindMapper
    {
        public GrpcV1.RuntimeHostObservationKind Map(
            Northbound.RuntimeHostObservationKind kind)
        {
            return (GrpcV1.RuntimeHostObservationKind)((int)kind + 1);
        }
    }

    private sealed class TestAttachmentMapper
        : IRuntimeHostAttachmentObservationPayloadMapper
    {
        public GrpcV1.AttachmentPublishedObservation Map(
            Northbound.RuntimeHostAttachmentPublishedObservationPayload payload)
        {
            return new GrpcV1.AttachmentPublishedObservation();
        }

        public GrpcV1.AttachmentEndedObservation Map(
            Northbound.RuntimeHostAttachmentEndedObservationPayload payload)
        {
            return new GrpcV1.AttachmentEndedObservation();
        }
    }

    private sealed class TestConnectionStatusMapper
        : IRuntimeHostConnectionStatusChangedObservationPayloadMapper
    {
        public GrpcV1.ConnectionStatusChangedObservation Map(
            Northbound.RuntimeHostConnectionStatusChangedObservationPayload
                payload)
        {
            return new GrpcV1.ConnectionStatusChangedObservation();
        }
    }

    private sealed class TestPropertyValueMapper
        : IRuntimeHostPropertyValueChangedObservationPayloadMapper
    {
        public GrpcV1.PropertyValueChangedObservation Map(
            Northbound.RuntimeHostPropertyValueChangedObservationPayload
                payload)
        {
            return new GrpcV1.PropertyValueChangedObservation();
        }
    }

    private sealed class TestEventMapper
        : IRuntimeHostEventOccurredObservationPayloadMapper
    {
        public GrpcV1.EventOccurredObservation Map(
            Northbound.RuntimeHostEventOccurredObservationPayload payload)
        {
            return new GrpcV1.EventOccurredObservation();
        }
    }
}
