using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class ObservationInitialSnapshotMapperTests
{
    [Fact]
    public void Constructor_NullSnapshotMapper_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "snapshotMapper",
            () =>
                new ObservationInitialSnapshotMapper(
                    null!));
    }

    [Fact]
    public void Map_NullSnapshot_ShouldThrow()
    {
        var mapper =
            CreateMapper();

        Assert.Throws<ArgumentNullException>(
            "snapshot",
            () =>
                mapper.Map(
                    null!,
                    new Northbound.RuntimeHostObservationSequence(
                        0)));
    }

    [Fact]
    public void Map_NullSequence_ShouldThrow()
    {
        var mapper =
            CreateMapper();

        Assert.Throws<ArgumentNullException>(
            "snapshotSequence",
            () =>
                mapper.Map(
                    CreateSnapshot(),
                    null!));
    }

    [Fact]
    public void Map_DefinedBoundary_ShouldCreateMandatoryInitialMessage()
    {
        var mapper =
            CreateMapper();
        Northbound.PublishedRuntimeHostSnapshot snapshot =
            CreateSnapshot();
        var sequence =
            new Northbound.RuntimeHostObservationSequence(
                42);

        GrpcV1.ObserveResponse result =
            mapper.Map(
                snapshot,
                sequence);

        Assert.Equal(
            GrpcV1.ObserveResponse.ContentOneofCase.InitialSnapshot,
            result.ContentCase);
        Assert.NotNull(
            result.InitialSnapshot);
        Assert.Equal(
            42UL,
            result.InitialSnapshot.SnapshotSequence);
        Assert.Equal(
            snapshot.RuntimeHostId.Value,
            result.InitialSnapshot.Snapshot.RuntimeHostId);
        Assert.Equal(
            snapshot.ApiVersion.Major,
            result.InitialSnapshot.Snapshot.ApiVersion.Major);
        Assert.Equal(
            snapshot.ApiVersion.Minor,
            result.InitialSnapshot.Snapshot.ApiVersion.Minor);
        Assert.Empty(
            result.InitialSnapshot.Snapshot.Endpoints);
    }

    private static ObservationInitialSnapshotMapper CreateMapper()
    {
        return new ObservationInitialSnapshotMapper(
            RuntimeHostSnapshotMapperFactory.Create());
    }

    private static Northbound.PublishedRuntimeHostSnapshot CreateSnapshot()
    {
        return new Northbound.PublishedRuntimeHostSnapshot(
            new Northbound.RuntimeHostId(
                "runtime-host-1"),
            Northbound.RuntimeHostApiVersion.Current,
            Array.Empty<Northbound.PublishedRuntimeEndpointSnapshot>());
    }
}
