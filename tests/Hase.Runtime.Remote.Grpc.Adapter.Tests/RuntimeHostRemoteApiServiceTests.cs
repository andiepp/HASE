using Northbound = global::Hase.Runtime.Northbound;
using GrpcV1 = global::Hase.Runtime.Remote.Grpc.V1;

namespace Hase.Runtime.Remote.Grpc.Adapter.Tests;

public sealed class RuntimeHostRemoteApiServiceTests
{
    [Fact]
    public void Constructor_NullDependency_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "snapshotProvider",
            () =>
                new RuntimeHostRemoteApiService(
                    null!,
                    RuntimeHostSnapshotMapperFactory.Create()));

        Assert.Throws<ArgumentNullException>(
            "snapshotMapper",
            () =>
                new RuntimeHostRemoteApiService(
                    new TestSnapshotProvider(
                        CreateSnapshot()),
                    null!));
    }

    [Fact]
    public async Task GetSnapshot_NullRequest_ShouldThrow()
    {
        var service =
            new RuntimeHostRemoteApiService(
                new TestSnapshotProvider(
                    CreateSnapshot()),
                RuntimeHostSnapshotMapperFactory.Create());

        await Assert.ThrowsAsync<ArgumentNullException>(
            "request",
            () =>
                service.GetSnapshot(
                    null!,
                    null!));
    }

    [Fact]
    public async Task GetSnapshot_ShouldCaptureAndMapAuthoritativeSnapshot()
    {
        var provider =
            new TestSnapshotProvider(
                CreateSnapshot());

        var service =
            new RuntimeHostRemoteApiService(
                provider,
                RuntimeHostSnapshotMapperFactory.Create());

        GrpcV1.GetSnapshotResponse response =
            await service.GetSnapshot(
                new GrpcV1.GetSnapshotRequest(),
                null!);

        Assert.Equal(
            1,
            provider.CaptureCount);
        Assert.Equal(
            "runtime-host-1",
            response.RuntimeHostId);
        Assert.Equal(
            1U,
            response.ApiVersion.Major);
        Assert.Equal(
            0U,
            response.ApiVersion.Minor);
        Assert.Empty(
            response.Endpoints);
    }

    [Fact]
    public async Task GetSnapshot_ProviderReturnsNull_ShouldThrow()
    {
        var service =
            new RuntimeHostRemoteApiService(
                new TestSnapshotProvider(
                    null!),
                RuntimeHostSnapshotMapperFactory.Create());

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    service.GetSnapshot(
                        new GrpcV1.GetSnapshotRequest(),
                        null!));

        Assert.Equal(
            "The runtime-host snapshot provider returned null.",
            exception.Message);
    }

    private static Northbound.PublishedRuntimeHostSnapshot CreateSnapshot()
    {
        return new Northbound.PublishedRuntimeHostSnapshot(
            new Northbound.RuntimeHostId(
                "runtime-host-1"),
            Northbound.RuntimeHostApiVersion.Current,
            Array.Empty<Northbound.PublishedRuntimeEndpointSnapshot>());
    }

    private sealed class TestSnapshotProvider
        : Northbound.IRuntimeHostSnapshotProvider
    {
        private readonly Northbound.PublishedRuntimeHostSnapshot snapshot;

        public TestSnapshotProvider(
            Northbound.PublishedRuntimeHostSnapshot snapshot)
        {
            this.snapshot =
                snapshot;
        }

        public int CaptureCount
        {
            get;
            private set;
        }

        public Northbound.PublishedRuntimeHostSnapshot Capture()
        {
            CaptureCount++;

            return snapshot;
        }
    }
}
