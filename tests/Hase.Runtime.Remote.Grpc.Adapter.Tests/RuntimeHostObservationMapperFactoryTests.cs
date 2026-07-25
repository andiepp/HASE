using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostObservationMapperFactoryTests
{
    [Fact]
    public void Create_ComposedMappers_ShouldMapCompleteObservationSurface()
    {
        RuntimeHostObservationMappers mappers =
            RuntimeHostObservationMapperFactory.Create();
        var snapshot =
            new Northbound.PublishedRuntimeHostSnapshot(
                new Northbound.RuntimeHostId(
                    "runtime-host-1"),
                Northbound.RuntimeHostApiVersion.Current,
                Array.Empty<Northbound.PublishedRuntimeEndpointSnapshot>());

        GrpcV1.ObserveResponse initial =
            mappers.InitialSnapshotMapper.Map(
                snapshot,
                new Northbound.RuntimeHostObservationSequence(
                    0));

        Assert.Equal(
            GrpcV1.ObserveResponse.ContentOneofCase.InitialSnapshot,
            initial.ContentCase);
        Assert.Equal(
            "runtime-host-1",
            initial.InitialSnapshot.Snapshot.RuntimeHostId);
        Assert.Equal(
            0UL,
            initial.InitialSnapshot.SnapshotSequence);

        var occurredAtUtc =
            new DateTimeOffset(
                2026,
                7,
                25,
                18,
                30,
                0,
                TimeSpan.Zero);
        var observation =
            new Northbound.RuntimeHostObservation(
                new Northbound.RuntimeHostObservationSequence(
                    1),
                new EndpointId(
                    "endpoint-01"),
                new Northbound.RuntimeEndpointAttachmentGeneration(
                    new Guid(
                        "868e79d4-b1a4-4a63-81cd-5a800d9ba3fd")),
                new Northbound.RuntimeHostEventOccurredObservationPayload(
                    new InstrumentId(
                        "controller-01"),
                    new DescriptorPath(
                        "Controller",
                        "ButtonPressed"),
                    occurredAtUtc,
                    "pressed"));

        GrpcV1.ObserveResponse mappedObservation =
            mappers.ObservationMapper.Map(
                observation);

        Assert.Equal(
            GrpcV1.ObserveResponse.ContentOneofCase.Observation,
            mappedObservation.ContentCase);
        Assert.Equal(
            GrpcV1.RuntimeHostObservationKind.EventOccurred,
            mappedObservation.Observation.Kind);
        Assert.Equal(
            1UL,
            mappedObservation.Observation.Sequence);
        Assert.Equal(
            "endpoint-01",
            mappedObservation.Observation.EndpointId);
        Assert.Equal(
            "controller-01",
            mappedObservation.Observation.EventOccurred.InstrumentId);
        Assert.Equal(
            "pressed",
            mappedObservation.Observation.EventOccurred.Value.StringValue);
    }
}
