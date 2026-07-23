using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Northbound;

namespace Hase.Runtime.Tests.Northbound;

public sealed class RuntimeHostSnapshotTests
{
    [Fact]
    public void PublishedSnapshot_StoresVersionAndCopiesEndpoints()
    {
        PublishedRuntimeEndpointSnapshot endpoint =
            CreateEndpointSnapshot(
                "snapshot-endpoint");

        var source =
            new List<PublishedRuntimeEndpointSnapshot>
            {
                endpoint
            };

        var snapshot =
            new PublishedRuntimeHostSnapshot(
                CreateRuntimeHostId(),
                new RuntimeHostApiVersion(
                    2,
                    1),
                source);

        source.Clear();

        Assert.Equal(
            CreateRuntimeHostId(),
            snapshot.RuntimeHostId);

        Assert.Equal(
            new RuntimeHostApiVersion(
                2,
                1),
            snapshot.ApiVersion);

        Assert.Same(
            endpoint,
            Assert.Single(
                snapshot.Endpoints));
    }

    [Fact]
    public void PublishedSnapshot_NullRuntimeHostId_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new PublishedRuntimeHostSnapshot(
                null!,
                RuntimeHostApiVersion.Current,
                Array.Empty<PublishedRuntimeEndpointSnapshot>()));
    }

    [Fact]
    public void PublishedSnapshot_NullEndpoints_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new PublishedRuntimeHostSnapshot(
                CreateRuntimeHostId(),
                RuntimeHostApiVersion.Current,
                null!));
    }

    [Fact]
    public void PublishedSnapshot_NullEndpointEntry_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new PublishedRuntimeHostSnapshot(
                CreateRuntimeHostId(),
                RuntimeHostApiVersion.Current,
                new PublishedRuntimeEndpointSnapshot[]
                {
                    null!
                }));
    }

    [Fact]
    public void Provider_NullRuntimeHostId_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostSnapshotProvider(
                null!,
                new TestInventorySnapshotProvider()));
    }

    [Fact]
    public void Provider_NullInventoryProvider_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostSnapshotProvider(
                CreateRuntimeHostId(),
                null!));
    }

    [Fact]
    public void Provider_CapturesCurrentApiVersionAndInventory()
    {
        PublishedRuntimeEndpointSnapshot endpoint =
            CreateEndpointSnapshot(
                "captured-endpoint");

        var provider =
            new RuntimeHostSnapshotProvider(
                CreateRuntimeHostId(),
                new TestInventorySnapshotProvider(
                    endpoint));

        PublishedRuntimeHostSnapshot snapshot =
            provider.Capture();

        Assert.Equal(
            CreateRuntimeHostId(),
            snapshot.RuntimeHostId);

        Assert.Equal(
            RuntimeHostApiVersion.Current,
            snapshot.ApiVersion);

        Assert.Same(
            endpoint,
            Assert.Single(
                snapshot.Endpoints));
    }

    [Fact]
    public void Provider_RepeatedCaptures_PreserveRuntimeHostIdentity()
    {
        RuntimeHostId runtimeHostId =
            CreateRuntimeHostId();

        var provider =
            new RuntimeHostSnapshotProvider(
                runtimeHostId,
                new TestInventorySnapshotProvider());

        PublishedRuntimeHostSnapshot firstSnapshot =
            provider.Capture();

        PublishedRuntimeHostSnapshot secondSnapshot =
            provider.Capture();

        Assert.Same(
            runtimeHostId,
            firstSnapshot.RuntimeHostId);

        Assert.Same(
            runtimeHostId,
            secondSnapshot.RuntimeHostId);
    }

    private static RuntimeHostId CreateRuntimeHostId()
    {
        return new RuntimeHostId(
            "runtime-host-58c50d84-c4ad-47a0-b7c6-1eeed3483593");
    }

    private static PublishedRuntimeEndpointSnapshot CreateEndpointSnapshot(
        string endpointId)
    {
        return new PublishedRuntimeEndpointSnapshot(
            RuntimeEndpointAttachmentGeneration.CreateNew(),
            new EndpointDescriptor(
                new EndpointId(
                    endpointId)),
            new EndpointConnectionStatus(
                EndpointConnectionState.Ready));
    }

    private sealed class TestInventorySnapshotProvider
        : IRuntimeHostInventorySnapshotProvider
    {
        private readonly IReadOnlyList<PublishedRuntimeEndpointSnapshot>
            _snapshots;

        public TestInventorySnapshotProvider(
            params PublishedRuntimeEndpointSnapshot[] snapshots)
        {
            _snapshots =
                snapshots.ToArray();
        }

        public IReadOnlyList<PublishedRuntimeEndpointSnapshot> List()
        {
            return _snapshots.ToArray();
        }

        public PublishedRuntimeEndpointSnapshot? Find(
            EndpointId endpointId)
        {
            return _snapshots.FirstOrDefault(
                snapshot =>
                    snapshot.EndpointId == endpointId);
        }
    }
}