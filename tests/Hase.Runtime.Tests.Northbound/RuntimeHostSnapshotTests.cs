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
                new RuntimeHostApiVersion(
                    2,
                    1),
                source);

        source.Clear();

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
    public void PublishedSnapshot_NullEndpoints_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new PublishedRuntimeHostSnapshot(
                RuntimeHostApiVersion.Current,
                null!));
    }

    [Fact]
    public void PublishedSnapshot_NullEndpointEntry_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new PublishedRuntimeHostSnapshot(
                RuntimeHostApiVersion.Current,
                new PublishedRuntimeEndpointSnapshot[]
                {
                    null!
                }));
    }

    [Fact]
    public void Provider_NullInventoryProvider_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostSnapshotProvider(
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
                new TestInventorySnapshotProvider(
                    endpoint));

        PublishedRuntimeHostSnapshot snapshot =
            provider.Capture();

        Assert.Equal(
            RuntimeHostApiVersion.Current,
            snapshot.ApiVersion);

        Assert.Same(
            endpoint,
            Assert.Single(
                snapshot.Endpoints));
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