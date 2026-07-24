using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Northbound;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;

namespace Hase.Runtime.Tests.Northbound;

public sealed class RuntimeHostNorthboundObservationCompositionTests
{
    [Fact]
    public async Task Composition_ExposesObservationService()
    {
        using var directory =
            new TemporaryDirectory();

        var inventory =
            new TestObservedInventory();

        await using RuntimeHostNorthboundSnapshotComposition composition =
            await CreateCompositionAsync(
                inventory,
                directory);

        Assert.NotNull(
            composition.ObservationService);

        Assert.IsAssignableFrom<IRuntimeHostObservationService>(
            composition.ObservationService);

        Assert.IsAssignableFrom<IAsyncDisposable>(
            composition);
    }

    [Fact]
    public async Task SnapshotAndObservation_UseSameGeneration()
    {
        using var directory =
            new TemporaryDirectory();

        var inventory =
            new TestObservedInventory();

        RuntimeEndpointAttachmentInventoryEntry initialEntry =
            CreateEntry(
                "initial-endpoint");

        inventory.Publish(
            initialEntry);

        await using RuntimeHostNorthboundSnapshotComposition composition =
            await CreateCompositionAsync(
                inventory,
                directory);

        PublishedRuntimeEndpointSnapshot initialEndpoint =
            Assert.Single(
                composition.InventorySnapshotProvider.List());

        await using RuntimeHostObservationSubscription subscription =
            await composition.ObservationService.OpenSubscriptionAsync(
                new RuntimeHostObservationSubscriptionOptions());

        PublishedRuntimeEndpointSnapshot observedInitialEndpoint =
            Assert.Single(
                subscription.InitialSnapshot.Endpoints);

        Assert.Equal(
            initialEndpoint.Generation,
            observedInitialEndpoint.Generation);

        RuntimeEndpointAttachmentInventoryEntry laterEntry =
            CreateEntry(
                "later-endpoint");

        inventory.Publish(
            laterEntry);

        RuntimeHostObservation observation =
            await ReadOneAsync(
                subscription);

        PublishedRuntimeEndpointSnapshot laterSnapshot =
            Assert.Single(
                composition.InventorySnapshotProvider
                    .List()
                    .Where(
                        endpoint =>
                            endpoint.EndpointId
                            == laterEntry.EndpointId));

        Assert.Equal(
            laterSnapshot.Generation,
            observation.AttachmentGeneration);
    }

    [Fact]
    public async Task CompositionDisposal_EndsExistingSubscriptions()
    {
        using var directory =
            new TemporaryDirectory();

        var inventory =
            new TestObservedInventory();

        RuntimeHostNorthboundSnapshotComposition composition =
            await CreateCompositionAsync(
                inventory,
                directory);

        RuntimeHostObservationSubscription subscription =
            await composition.ObservationService.OpenSubscriptionAsync(
                new RuntimeHostObservationSubscriptionOptions());

        await composition.DisposeAsync();
        await composition.DisposeAsync();

        await foreach (
            RuntimeHostObservation observation
            in subscription.ReadAllAsync())
        {
            _ =
                observation;
        }

        Assert.False(
            inventory.IsDisposed);

        Assert.Equal(
            1,
            inventory.RegistrationDisposeCount);

        await subscription.DisposeAsync();
    }

    [Fact]
    public async Task CompositionDisposal_RejectsNewSubscriptions()
    {
        using var directory =
            new TemporaryDirectory();

        var inventory =
            new TestObservedInventory();

        RuntimeHostNorthboundSnapshotComposition composition =
            await CreateCompositionAsync(
                inventory,
                directory);

        IRuntimeHostObservationService observationService =
            composition.ObservationService;

        await composition.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => observationService.OpenSubscriptionAsync(
                new RuntimeHostObservationSubscriptionOptions()));

        Assert.False(
            inventory.IsDisposed);
    }

    [Fact]
    public async Task CompositionDisposal_DoesNotEndInventoryAttachments()
    {
        using var directory =
            new TemporaryDirectory();

        var inventory =
            new TestObservedInventory();

        RuntimeEndpointAttachmentInventoryEntry entry =
            CreateEntry(
                "endpoint-one");

        inventory.Publish(
            entry);

        RuntimeHostNorthboundSnapshotComposition composition =
            await CreateCompositionAsync(
                inventory,
                directory);

        await composition.DisposeAsync();

        Assert.Same(
            entry,
            Assert.Single(
                inventory.List()));

        Assert.False(
            inventory.IsDisposed);
    }

    private static Task<RuntimeHostNorthboundSnapshotComposition>
        CreateCompositionAsync(
            TestObservedInventory inventory,
            TemporaryDirectory directory)
    {
        return RuntimeHostNorthboundSnapshotComposition
            .CreateFileBackedAsync(
                inventory,
                Path.Combine(
                    directory.Path,
                    "runtime-host-identity.json"),
                new RuntimeHostId(
                    "runtime-host-observation-composition"));
    }

    private static async Task<RuntimeHostObservation> ReadOneAsync(
        RuntimeHostObservationSubscription subscription)
    {
        using var cancellationSource =
            new CancellationTokenSource(
                TimeSpan.FromSeconds(
                    3));

        await using IAsyncEnumerator<RuntimeHostObservation> enumerator =
            subscription
                .ReadAllAsync(
                    cancellationSource.Token)
                .GetAsyncEnumerator();

        Assert.True(
            await enumerator.MoveNextAsync());

        return enumerator.Current;
    }

    private static RuntimeEndpointAttachmentInventoryEntry CreateEntry(
        string endpointId)
    {
        var endpoint =
            new RuntimeEndpoint(
                new RuntimeContext(),
                new EndpointDescriptor(
                    new EndpointId(
                        endpointId)));

        return new RuntimeEndpointAttachmentInventoryEntry(
            new TestAttachmentSession(
                endpoint));
    }

    private sealed class TestObservedInventory
        : IRuntimeEndpointAttachmentInventory,
          IRuntimeEndpointAttachmentInventoryObservationSource
    {
        private readonly List<RuntimeEndpointAttachmentInventoryEntry>
            _entries =
                [];

        private IRuntimeEndpointAttachmentInventoryObserver? _observer;

        public bool IsDisposed
        {
            get;
            private set;
        }

        public int RegistrationDisposeCount
        {
            get;
            private set;
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

        public IDisposable Subscribe(
            IRuntimeEndpointAttachmentInventoryObserver observer)
        {
            _observer =
                observer;

            return new DelegateDisposable(
                () =>
                {
                    RegistrationDisposeCount++;
                    _observer =
                        null;
                });
        }

        public ValueTask DisposeAsync()
        {
            IsDisposed =
                true;

            return ValueTask.CompletedTask;
        }

        public void Publish(
            RuntimeEndpointAttachmentInventoryEntry entry)
        {
            _entries.Add(
                entry);

            _observer?.OnAttachmentPublished(
                new RuntimeEndpointAttachmentPublished(
                    entry));
        }
    }

    private sealed class TestAttachmentSession
        : IEndpointAttachmentSession
    {
        public TestAttachmentSession(
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

    private sealed class DelegateDisposable
        : IDisposable
    {
        private Action? _dispose;

        public DelegateDisposable(
            Action dispose)
        {
            _dispose =
                dispose;
        }

        public void Dispose()
        {
            Interlocked.Exchange(
                    ref _dispose,
                    null)
                ?.Invoke();
        }
    }

    private sealed class TemporaryDirectory
        : IDisposable
    {
        public TemporaryDirectory()
        {
            Path =
                System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    $"hase-observation-composition-{Guid.NewGuid():N}");

            Directory.CreateDirectory(
                Path);
        }

        public string Path
        {
            get;
        }

        public void Dispose()
        {
            if (Directory.Exists(
                    Path))
            {
                Directory.Delete(
                    Path,
                    recursive: true);
            }
        }
    }
}