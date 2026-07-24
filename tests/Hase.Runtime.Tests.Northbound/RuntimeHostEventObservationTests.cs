using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Events;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Northbound;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;

namespace Hase.Runtime.Tests.Northbound;

public sealed class RuntimeHostEventObservationTests
{
    [Fact]
    public async Task CurrentAttachment_EventOccurrence_IsObserved()
    {
        var context =
            CreateContext();

        RuntimeEndpointAttachmentInventoryEntry entry =
            CreateEntry(
                "endpoint-one");

        context.Inventory.Publish(
            entry);

        await using RuntimeHostObservationSubscription subscription =
            await context.Service.OpenSubscriptionAsync(
                new RuntimeHostObservationSubscriptionOptions());

        DateTimeOffset occurredAtUtc =
            new(
                2026,
                7,
                24,
                19,
                0,
                0,
                TimeSpan.Zero);

        object value =
            true;

        GetEvent(
                entry)
            .PublishOccurrence(
                occurredAtUtc,
                value);

        RuntimeHostObservation observation =
            await ReadOneAsync(
                subscription);

        Assert.Equal(
            RuntimeHostObservationKind.EventOccurred,
            observation.Kind);

        var payload =
            Assert.IsType<RuntimeHostEventOccurredObservationPayload>(
                observation.Payload);

        Assert.Equal(
            new InstrumentId(
                "instrument-one"),
            payload.InstrumentId);

        Assert.Equal(
            new DescriptorPath(
                "Button",
                "Pressed"),
            payload.EventPath);

        Assert.Equal(
            occurredAtUtc,
            payload.OccurredAtUtc);

        Assert.Same(
            value,
            payload.Value);

        Assert.Equal(
            Assert.Single(
                    subscription.InitialSnapshot.Endpoints)
                .Generation,
            observation.AttachmentGeneration);

        await context.DisposeAsync();
    }

    [Fact]
    public async Task PublishedAttachment_EventOccurrence_UsesPublishedGeneration()
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

        GetEvent(
                entry)
            .PublishOccurrence(
                DateTimeOffset.UtcNow,
                null);

        RuntimeHostObservation occurrence =
            await ReadOneAsync(
                subscription);

        Assert.Equal(
            RuntimeHostObservationKind.AttachmentPublished,
            publication.Kind);

        Assert.Equal(
            RuntimeHostObservationKind.EventOccurred,
            occurrence.Kind);

        Assert.Equal(
            publication.AttachmentGeneration,
            occurrence.AttachmentGeneration);

        Assert.Null(
            Assert.IsType<RuntimeHostEventOccurredObservationPayload>(
                    occurrence.Payload)
                .Value);

        await context.DisposeAsync();
    }

    [Fact]
    public async Task NewSubscription_DoesNotReplayEarlierOccurrence()
    {
        var context =
            CreateContext();

        RuntimeEndpointAttachmentInventoryEntry entry =
            CreateEntry(
                "endpoint-one");

        context.Inventory.Publish(
            entry);

        GetEvent(
                entry)
            .PublishOccurrence(
                DateTimeOffset.UtcNow,
                true);

        await using RuntimeHostObservationSubscription subscription =
            await context.Service.OpenSubscriptionAsync(
                new RuntimeHostObservationSubscriptionOptions());

        Assert.Single(
            subscription.InitialSnapshot.Endpoints);

        using var cancellationSource =
            new CancellationTokenSource(
                TimeSpan.FromMilliseconds(
                    100));

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
    public async Task EndedAttachment_LaterEventCallback_IsSuppressed()
    {
        var context =
            CreateContext();

        RuntimeEndpointAttachmentInventoryEntry entry =
            CreateEntry(
                "endpoint-one");

        context.Inventory.Publish(
            entry);

        await using RuntimeHostObservationSubscription subscription =
            await context.Service.OpenSubscriptionAsync(
                new RuntimeHostObservationSubscriptionOptions());

        context.Inventory.End(
            entry,
            DateTimeOffset.UtcNow);

        RuntimeHostObservation ending =
            await ReadOneAsync(
                subscription);

        GetEvent(
                entry)
            .PublishOccurrence(
                DateTimeOffset.UtcNow,
                true);

        Assert.Equal(
            RuntimeHostObservationKind.AttachmentEnded,
            ending.Kind);

        using var cancellationSource =
            new CancellationTokenSource(
                TimeSpan.FromMilliseconds(
                    100));

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

    private static TestContext CreateContext()
    {
        var inventory =
            new TestObservedInventory();

        var projection =
            new RuntimeHostAttachmentProjection(
                inventory,
                inventory);

        object? instance =
            Activator.CreateInstance(
                typeof(RuntimeHostObservationService),
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic,
                binder: null,
                args:
                [
                    new RuntimeHostId(
                        "runtime-host-event-observation-tests"),
                    projection
                ],
                culture: null);

        return new TestContext(
            inventory,
            projection,
            Assert.IsType<RuntimeHostObservationService>(
                instance));
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
        var eventDescriptor =
            new EventDescriptor(
                new DescriptorPath(
                    "Button",
                    "Pressed"),
                "Button pressed");

        var instrumentDescriptor =
            new InstrumentDescriptor(
                new InstrumentId(
                    "instrument-one"),
                "Instrument",
                new InstrumentKind(
                    "test"))
            {
                Interface =
                    new InstrumentInterface(
                        events:
                        [
                            eventDescriptor
                        ])
            };

        var endpoint =
            new RuntimeEndpoint(
                new RuntimeContext(),
                new EndpointDescriptor(
                    new EndpointId(
                        endpointId),
                    [
                        instrumentDescriptor
                    ]));

        return new RuntimeEndpointAttachmentInventoryEntry(
            new TestAttachmentSession(
                endpoint));
    }

    private static RuntimeEvent GetEvent(
        RuntimeEndpointAttachmentInventoryEntry entry)
    {
        return Assert.Single(
            Assert.Single(
                    entry.RuntimeEndpoint.Instruments)
                .Events);
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

        public Task<RuntimeEndpointAttachmentInventoryEntry> AttachAsync(
            EndpointAttachmentRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public RuntimeEndpointAttachmentInventoryEntry? Find(
            EndpointId endpointId) =>
            _entries.FirstOrDefault(
                entry =>
                    entry.EndpointId == endpointId);

        public IReadOnlyList<RuntimeEndpointAttachmentInventoryEntry> List() =>
            _entries.ToArray();

        public Task<bool> DetachAsync(
            EndpointId endpointId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

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

        public ValueTask DisposeAsync() =>
            ValueTask.CompletedTask;

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
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() =>
            ValueTask.CompletedTask;
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