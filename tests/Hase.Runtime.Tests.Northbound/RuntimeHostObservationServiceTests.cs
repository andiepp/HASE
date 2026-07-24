using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Northbound;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;

namespace Hase.Runtime.Tests.Northbound;

public sealed class RuntimeHostObservationServiceTests
{
    [Fact]
    public async Task OpenSubscriptionAsync_CapturesCurrentAttachments()
    {
        var inventory =
            new TestObservedInventory();

        RuntimeEndpointAttachmentInventoryEntry entry =
            CreateEntry(
                "endpoint-one");

        inventory.Publish(
            entry);

        using var projection =
            new RuntimeHostAttachmentProjection(
                inventory,
                inventory);

        await using var service =
            new RuntimeHostObservationService(
                CreateRuntimeHostId(),
                projection);

        await using RuntimeHostObservationSubscription subscription =
            await service.OpenSubscriptionAsync(
                new RuntimeHostObservationSubscriptionOptions());

        PublishedRuntimeEndpointSnapshot endpoint =
            Assert.Single(
                subscription.InitialSnapshot.Endpoints);

        Assert.Equal(
            entry.EndpointId,
            endpoint.EndpointId);

        Assert.Equal(
            new RuntimeHostObservationSequence(
                0),
            subscription.SnapshotSequence);
    }

    [Fact]
    public async Task PublicationAfterSnapshot_IsDeliveredWithFirstSequence()
    {
        var context =
            CreateContext();

        await using RuntimeHostObservationSubscription subscription =
            await context.Service.OpenSubscriptionAsync(
                new RuntimeHostObservationSubscriptionOptions());

        RuntimeEndpointAttachmentInventoryEntry entry =
            CreateEntry(
                "endpoint-one");

        context.Inventory.Publish(
            entry);

        RuntimeHostObservation observation =
            await ReadOneAsync(
                subscription);

        Assert.Equal(
            new RuntimeHostObservationSequence(
                1),
            observation.Sequence);

        Assert.Equal(
            RuntimeHostObservationKind.AttachmentPublished,
            observation.Kind);

        Assert.Equal(
            entry.EndpointId,
            observation.EndpointId);

        await context.DisposeAsync();
    }

    [Fact]
    public async Task Ending_PreservesGenerationAndAdvancesSequence()
    {
        var context =
            CreateContext();

        await using RuntimeHostObservationSubscription subscription =
            await context.Service.OpenSubscriptionAsync(
                new RuntimeHostObservationSubscriptionOptions());

        RuntimeEndpointAttachmentInventoryEntry entry =
            CreateEntry(
                "endpoint-one");

        context.Inventory.Publish(
            entry);

        RuntimeHostObservation publication =
            await ReadOneAsync(
                subscription);

        DateTimeOffset endedAtUtc =
            new(
                2026,
                7,
                24,
                17,
                30,
                0,
                TimeSpan.Zero);

        context.Inventory.End(
            entry,
            endedAtUtc);

        RuntimeHostObservation ending =
            await ReadOneAsync(
                subscription);

        Assert.Equal(
            new RuntimeHostObservationSequence(
                2),
            ending.Sequence);

        Assert.Equal(
            RuntimeHostObservationKind.AttachmentEnded,
            ending.Kind);

        Assert.Equal(
            publication.AttachmentGeneration,
            ending.AttachmentGeneration);

        var payload =
            Assert.IsType<RuntimeHostAttachmentEndedObservationPayload>(
                ending.Payload);

        Assert.Equal(
            endedAtUtc,
            payload.EndedAtUtc);

        await context.DisposeAsync();
    }

    [Fact]
    public async Task FullBuffer_TerminatesWithObservationGap()
    {
        var context =
            CreateContext();

        await using RuntimeHostObservationSubscription subscription =
            await context.Service.OpenSubscriptionAsync(
                new RuntimeHostObservationSubscriptionOptions(
                    1));

        context.Inventory.Publish(
            CreateEntry(
                "endpoint-one"));

        context.Inventory.Publish(
            CreateEntry(
                "endpoint-two"));

        await Assert.ThrowsAsync<RuntimeHostObservationGapException>(
            async () =>
            {
                await foreach (
                    RuntimeHostObservation observation
                    in subscription.ReadAllAsync())
                {
                    _ =
                        observation;
                }
            });

        await context.DisposeAsync();
    }

    [Fact]
    public async Task SlowSubscriptionGap_DoesNotAffectHealthySubscription()
    {
        var context =
            CreateContext();

        await using RuntimeHostObservationSubscription slowSubscription =
            await context.Service.OpenSubscriptionAsync(
                new RuntimeHostObservationSubscriptionOptions(
                    1));

        await using RuntimeHostObservationSubscription healthySubscription =
            await context.Service.OpenSubscriptionAsync(
                new RuntimeHostObservationSubscriptionOptions(
                    4));

        context.Inventory.Publish(
            CreateEntry(
                "endpoint-one"));

        context.Inventory.Publish(
            CreateEntry(
                "endpoint-two"));

        RuntimeHostObservation first =
            await ReadOneAsync(
                healthySubscription);

        RuntimeHostObservation second =
            await ReadOneAsync(
                healthySubscription);

        Assert.Equal(
            1,
            first.Sequence.Value);

        Assert.Equal(
            2,
            second.Sequence.Value);

        await context.DisposeAsync();
    }

    [Fact]
    public async Task EnumerationCancellation_Propagates()
    {
        var context =
            CreateContext();

        await using RuntimeHostObservationSubscription subscription =
            await context.Service.OpenSubscriptionAsync(
                new RuntimeHostObservationSubscriptionOptions());

        using var cancellationSource =
            new CancellationTokenSource();

        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () =>
            {
                await foreach (
                    RuntimeHostObservation observation
                    in subscription.ReadAllAsync(
                        cancellationSource.Token))
                {
                    _ =
                        observation;
                }
            });

        await context.DisposeAsync();
    }

    [Fact]
    public async Task SubscriptionDisposal_StopsOnlyThatSubscription()
    {
        var context =
            CreateContext();

        RuntimeHostObservationSubscription disposedSubscription =
            await context.Service.OpenSubscriptionAsync(
                new RuntimeHostObservationSubscriptionOptions());

        await using RuntimeHostObservationSubscription activeSubscription =
            await context.Service.OpenSubscriptionAsync(
                new RuntimeHostObservationSubscriptionOptions());

        await disposedSubscription.DisposeAsync();
        await disposedSubscription.DisposeAsync();

        context.Inventory.Publish(
            CreateEntry(
                "endpoint-one"));

        RuntimeHostObservation observation =
            await ReadOneAsync(
                activeSubscription);

        Assert.Equal(
            RuntimeHostObservationKind.AttachmentPublished,
            observation.Kind);

        await context.DisposeAsync();
    }

    [Fact]
    public async Task ServiceDisposal_EndsSubscriptionsWithoutDisposingInventory()
    {
        var context =
            CreateContext();

        RuntimeHostObservationSubscription subscription =
            await context.Service.OpenSubscriptionAsync(
                new RuntimeHostObservationSubscriptionOptions());

        await context.Service.DisposeAsync();
        await context.Service.DisposeAsync();

        await foreach (
            RuntimeHostObservation observation
            in subscription.ReadAllAsync())
        {
            _ =
                observation;
        }

        Assert.False(
            context.Inventory.IsDisposed);

        await subscription.DisposeAsync();
        context.Projection.Dispose();
    }

    [Fact]
    public async Task OpenSubscriptionAsync_PreCancelled_Throws()
    {
        var context =
            CreateContext();

        using var cancellationSource =
            new CancellationTokenSource();

        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => context.Service.OpenSubscriptionAsync(
                new RuntimeHostObservationSubscriptionOptions(),
                cancellationSource.Token));

        await context.DisposeAsync();
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

    private static TestContext CreateContext()
    {
        var inventory =
            new TestObservedInventory();

        var projection =
            new RuntimeHostAttachmentProjection(
                inventory,
                inventory);

        var service =
            new RuntimeHostObservationService(
                CreateRuntimeHostId(),
                projection);

        return new TestContext(
            inventory,
            projection,
            service);
    }

    private static RuntimeHostId CreateRuntimeHostId()
    {
        return new RuntimeHostId(
            "runtime-host-observation-tests");
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

    private sealed record TestContext(
        TestObservedInventory Inventory,
        RuntimeHostAttachmentProjection Projection,
        RuntimeHostObservationService Service)
        : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await Service.DisposeAsync();
            Projection.Dispose();
        }
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
                    _observer =
                        null);
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

        public void End(
            RuntimeEndpointAttachmentInventoryEntry entry,
            DateTimeOffset endedAtUtc)
        {
            _entries.Remove(
                entry);

            _observer?.OnAttachmentEnded(
                new RuntimeEndpointAttachmentEnded(
                    entry,
                    endedAtUtc));
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
}