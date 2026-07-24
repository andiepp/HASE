using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;
using Hase.Runtime.Northbound;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;

namespace Hase.Runtime.Tests.Northbound;

public sealed class RuntimeHostInventorySnapshotProviderTests
{
    [Fact]
    public void Constructor_NullInventory_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostInventorySnapshotProvider(
                (IRuntimeEndpointAttachmentInventory)null!));
    }

    [Fact]
    public void List_ProjectsAuthoritativePublishedEntry()
    {
        RuntimeEndpointAttachmentInventoryEntry entry =
            CreateEntry(
                "projected-endpoint");

        entry.RuntimeEndpoint.UpdateConnectionStatus(
            new EndpointConnectionStatus(
                EndpointConnectionState.Ready));

        var inventory =
            new TestAttachmentInventory(
                entry);

        var provider =
            new RuntimeHostInventorySnapshotProvider(
                inventory);

        PublishedRuntimeEndpointSnapshot snapshot =
            Assert.Single(
                provider.List());

        Assert.Equal(
            entry.EndpointId,
            snapshot.EndpointId);

        Assert.Same(
            entry.RuntimeEndpoint.Descriptor,
            snapshot.Descriptor);

        Assert.Same(
            entry.RuntimeEndpoint.ConnectionStatus,
            snapshot.ConnectionStatus);

        Assert.NotEqual(
            Guid.Empty,
            snapshot.Generation.Value);
    }

    [Fact]
    public void List_SameEntry_PreservesGenerationAndRefreshesState()
    {
        RuntimeEndpointAttachmentInventoryEntry entry =
            CreateEntry(
                "stable-entry");

        var inventory =
            new TestAttachmentInventory(
                entry);

        var provider =
            new RuntimeHostInventorySnapshotProvider(
                inventory);

        PublishedRuntimeEndpointSnapshot firstSnapshot =
            Assert.Single(
                provider.List());

        entry.RuntimeEndpoint.UpdateConnectionStatus(
            new EndpointConnectionStatus(
                EndpointConnectionState.Ready));

        PublishedRuntimeEndpointSnapshot secondSnapshot =
            Assert.Single(
                provider.List());

        Assert.Equal(
            firstSnapshot.Generation,
            secondSnapshot.Generation);

        Assert.Equal(
            EndpointConnectionState.Disconnected,
            firstSnapshot.ConnectionStatus.State);

        Assert.Equal(
            EndpointConnectionState.Ready,
            secondSnapshot.ConnectionStatus.State);
    }

    [Fact]
    public void List_ReattachedIdentity_UsesNewGeneration()
    {
        RuntimeEndpointAttachmentInventoryEntry firstEntry =
            CreateEntry(
                "reattached-endpoint");

        var inventory =
            new TestAttachmentInventory(
                firstEntry);

        var provider =
            new RuntimeHostInventorySnapshotProvider(
                inventory);

        PublishedRuntimeEndpointSnapshot firstSnapshot =
            Assert.Single(
                provider.List());

        inventory.SetEntries();

        Assert.Empty(
            provider.List());

        RuntimeEndpointAttachmentInventoryEntry secondEntry =
            CreateEntry(
                "reattached-endpoint");

        inventory.SetEntries(
            secondEntry);

        PublishedRuntimeEndpointSnapshot secondSnapshot =
            Assert.Single(
                provider.List());

        Assert.Equal(
            firstSnapshot.EndpointId,
            secondSnapshot.EndpointId);

        Assert.NotEqual(
            firstSnapshot.Generation,
            secondSnapshot.Generation);
    }

    [Fact]
    public void List_ReturnsSnapshotCollection()
    {
        RuntimeEndpointAttachmentInventoryEntry firstEntry =
            CreateEntry(
                "first-endpoint");

        var inventory =
            new TestAttachmentInventory(
                firstEntry);

        var provider =
            new RuntimeHostInventorySnapshotProvider(
                inventory);

        IReadOnlyList<PublishedRuntimeEndpointSnapshot> snapshot =
            provider.List();

        inventory.SetEntries(
            firstEntry,
            CreateEntry(
                "second-endpoint"));

        Assert.Single(
            snapshot);

        Assert.Equal(
            2,
            provider.List().Count);
    }

    [Fact]
    public void Find_PublishedIdentity_ReturnsSameGenerationAsList()
    {
        RuntimeEndpointAttachmentInventoryEntry entry =
            CreateEntry(
                "lookup-endpoint");

        var provider =
            new RuntimeHostInventorySnapshotProvider(
                new TestAttachmentInventory(
                    entry));

        PublishedRuntimeEndpointSnapshot listedSnapshot =
            Assert.Single(
                provider.List());

        PublishedRuntimeEndpointSnapshot? foundSnapshot =
            provider.Find(
                entry.EndpointId);

        Assert.NotNull(
            foundSnapshot);

        Assert.Equal(
            listedSnapshot.EndpointId,
            foundSnapshot.EndpointId);

        Assert.Equal(
            listedSnapshot.Generation,
            foundSnapshot.Generation);
    }

    [Fact]
    public void Find_MissingIdentity_ReturnsNull()
    {
        var provider =
            new RuntimeHostInventorySnapshotProvider(
                new TestAttachmentInventory(
                    CreateEntry(
                        "published-endpoint")));

        PublishedRuntimeEndpointSnapshot? snapshot =
            provider.Find(
                new EndpointId(
                    "missing-endpoint"));

        Assert.Null(
            snapshot);
    }

    [Fact]
    public void Find_AfterEntryEnds_ReturnsNull()
    {
        RuntimeEndpointAttachmentInventoryEntry entry =
            CreateEntry(
                "ended-endpoint");

        var inventory =
            new TestAttachmentInventory(
                entry);

        var provider =
            new RuntimeHostInventorySnapshotProvider(
                inventory);

        Assert.NotNull(
            provider.Find(
                entry.EndpointId));

        inventory.SetEntries();

        Assert.Null(
            provider.Find(
                entry.EndpointId));
    }

    [Fact]
    public void SharedProjection_ProvidersUseSameGeneration()
    {
        RuntimeEndpointAttachmentInventoryEntry entry =
            CreateEntry(
                "shared-projection-endpoint");

        var projection =
            new RuntimeHostAttachmentProjection(
                new TestAttachmentInventory(
                    entry));

        var firstProvider =
            RuntimeHostInventorySnapshotProvider.CreateShared(
                projection);

        var secondProvider =
            RuntimeHostInventorySnapshotProvider.CreateShared(
                projection);

        Assert.Equal(
            Assert.Single(
                    firstProvider.List())
                .Generation,
            Assert.Single(
                    secondProvider.List())
                .Generation);
    }

    [Fact]
    public void SharedProjection_ReattachedEntryReceivesNewGeneration()
    {
        RuntimeEndpointAttachmentInventoryEntry firstEntry =
            CreateEntry(
                "shared-reattached-endpoint");

        var inventory =
            new TestAttachmentInventory(
                firstEntry);

        var projection =
            new RuntimeHostAttachmentProjection(
                inventory);

        RuntimeHostPublishedAttachment firstAttachment =
            Assert.Single(
                projection.List());

        inventory.SetEntries();

        Assert.Empty(
            projection.List());

        RuntimeEndpointAttachmentInventoryEntry secondEntry =
            CreateEntry(
                "shared-reattached-endpoint");

        inventory.SetEntries(
            secondEntry);

        RuntimeHostPublishedAttachment secondAttachment =
            Assert.Single(
                projection.List());

        Assert.NotEqual(
            firstAttachment.Generation,
            secondAttachment.Generation);

        Assert.Same(
            secondEntry,
            projection.Find(
                    secondEntry.EndpointId)
                ?.Entry);
    }

    private static RuntimeEndpointAttachmentInventoryEntry CreateEntry(
        string endpointId)
    {
        var runtimeEndpoint =
            new RuntimeEndpoint(
                new RuntimeContext(),
                new EndpointDescriptor(
                    new EndpointId(
                        endpointId)));

        return new RuntimeEndpointAttachmentInventoryEntry(
            new TestEndpointAttachmentSession(
                runtimeEndpoint));
    }

    private sealed class TestAttachmentInventory
        : IRuntimeEndpointAttachmentInventory
    {
        private IReadOnlyList<RuntimeEndpointAttachmentInventoryEntry>
            _entries;

        public TestAttachmentInventory(
            params RuntimeEndpointAttachmentInventoryEntry[] entries)
        {
            _entries =
                entries.ToArray();
        }

        public Task<RuntimeEndpointAttachmentInventoryEntry> AttachAsync(
            EndpointAttachmentRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public RuntimeEndpointAttachmentInventoryEntry? Find(
            EndpointId endpointId)
        {
            return _entries.FirstOrDefault(
                entry =>
                    entry.EndpointId == endpointId);
        }

        public IReadOnlyList<RuntimeEndpointAttachmentInventoryEntry> List()
        {
            return _entries.ToArray();
        }

        public Task<bool> DetachAsync(
            EndpointId endpointId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public void SetEntries(
            params RuntimeEndpointAttachmentInventoryEntry[] entries)
        {
            _entries =
                entries.ToArray();
        }
    }

    private sealed class TestEndpointAttachmentSession
        : IEndpointAttachmentSession
    {
        public TestEndpointAttachmentSession(
            RuntimeEndpoint runtimeEndpoint)
        {
            RuntimeEndpoint =
                runtimeEndpoint;

            Request =
                null!;
        }

        public EndpointAttachmentRequest Request
        {
            get;
        }

        public RuntimeEndpoint RuntimeEndpoint
        {
            get;
        }

        public Task ShutdownAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
