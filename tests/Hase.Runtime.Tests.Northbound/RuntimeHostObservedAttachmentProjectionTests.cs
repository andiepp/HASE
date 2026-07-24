using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Northbound;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;

namespace Hase.Runtime.Tests.Northbound;

public sealed class RuntimeHostObservedAttachmentProjectionTests
{
    [Fact]
    public void Constructor_NullObservationSource_Throws()
    {
        var inventory =
            new TestObservedInventory();

        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostAttachmentProjection(
                inventory,
                null!));
    }

    [Fact]
    public void Publication_AssignsGenerationAndPublishesOrderedChange()
    {
        var inventory =
            new TestObservedInventory();

        using var projection =
            new RuntimeHostAttachmentProjection(
                inventory,
                inventory);

        var observer =
            new RecordingProjectionObserver();

        using IDisposable registration =
            projection.Subscribe(
                observer);

        RuntimeEndpointAttachmentInventoryEntry entry =
            CreateEntry(
                "endpoint-one");

        inventory.Publish(
            entry);

        RuntimeHostPublishedAttachment attachment =
            Assert.Single(
                projection.List());

        RuntimeHostAttachmentProjectionChange change =
            Assert.Single(
                observer.Changes);

        Assert.Equal(
            1,
            change.Order);

        Assert.Equal(
            RuntimeHostAttachmentProjectionChangeKind.Published,
            change.Kind);

        Assert.Same(
            entry,
            change.Attachment.Entry);

        Assert.Equal(
            attachment.Generation,
            change.Attachment.Generation);

        Assert.Null(
            change.EndedAtUtc);
    }

    [Fact]
    public void Ending_RetiresSameGenerationAndPreservesOrder()
    {
        var inventory =
            new TestObservedInventory();

        using var projection =
            new RuntimeHostAttachmentProjection(
                inventory,
                inventory);

        var observer =
            new RecordingProjectionObserver();

        using IDisposable registration =
            projection.Subscribe(
                observer);

        RuntimeEndpointAttachmentInventoryEntry entry =
            CreateEntry(
                "endpoint-one");

        inventory.Publish(
            entry);

        RuntimeEndpointAttachmentGeneration generation =
            Assert.Single(
                    projection.List())
                .Generation;

        DateTimeOffset endedAtUtc =
            new(
                2026,
                7,
                24,
                17,
                15,
                0,
                TimeSpan.Zero);

        inventory.End(
            entry,
            endedAtUtc);

        Assert.Empty(
            projection.List());

        Assert.Collection(
            observer.Changes,
            publication =>
            {
                Assert.Equal(
                    1,
                    publication.Order);

                Assert.Equal(
                    RuntimeHostAttachmentProjectionChangeKind.Published,
                    publication.Kind);
            },
            ending =>
            {
                Assert.Equal(
                    2,
                    ending.Order);

                Assert.Equal(
                    RuntimeHostAttachmentProjectionChangeKind.Ended,
                    ending.Kind);

                Assert.Equal(
                    generation,
                    ending.Attachment.Generation);

                Assert.Equal(
                    endedAtUtc,
                    ending.EndedAtUtc);
            });
    }

    [Fact]
    public void ReattachedIdentity_ReceivesNewGeneration()
    {
        var inventory =
            new TestObservedInventory();

        using var projection =
            new RuntimeHostAttachmentProjection(
                inventory,
                inventory);

        RuntimeEndpointAttachmentInventoryEntry firstEntry =
            CreateEntry(
                "endpoint-one");

        inventory.Publish(
            firstEntry);

        RuntimeEndpointAttachmentGeneration firstGeneration =
            Assert.Single(
                    projection.List())
                .Generation;

        inventory.End(
            firstEntry,
            DateTimeOffset.UtcNow);

        RuntimeEndpointAttachmentInventoryEntry secondEntry =
            CreateEntry(
                "endpoint-one");

        inventory.Publish(
            secondEntry);

        RuntimeHostPublishedAttachment secondAttachment =
            Assert.Single(
                projection.List());

        Assert.NotEqual(
            firstGeneration,
            secondAttachment.Generation);

        Assert.Same(
            secondEntry,
            secondAttachment.Entry);
    }

    [Fact]
    public void StaleEnding_DoesNotRetireReplacementEntry()
    {
        var inventory =
            new TestObservedInventory();

        using var projection =
            new RuntimeHostAttachmentProjection(
                inventory,
                inventory);

        var observer =
            new RecordingProjectionObserver();

        using IDisposable registration =
            projection.Subscribe(
                observer);

        RuntimeEndpointAttachmentInventoryEntry firstEntry =
            CreateEntry(
                "endpoint-one");

        inventory.Publish(
            firstEntry);

        inventory.End(
            firstEntry,
            DateTimeOffset.UtcNow);

        RuntimeEndpointAttachmentInventoryEntry secondEntry =
            CreateEntry(
                "endpoint-one");

        inventory.Publish(
            secondEntry);

        int changeCountBeforeStaleEnding =
            observer.Changes.Count;

        inventory.NotifyStaleEnding(
            firstEntry,
            DateTimeOffset.UtcNow);

        Assert.Equal(
            changeCountBeforeStaleEnding,
            observer.Changes.Count);

        Assert.Same(
            secondEntry,
            Assert.Single(
                    projection.List())
                .Entry);
    }

    [Fact]
    public void ObserverFailure_DoesNotPreventLaterObserver()
    {
        var inventory =
            new TestObservedInventory();

        using var projection =
            new RuntimeHostAttachmentProjection(
                inventory,
                inventory);

        using IDisposable failingRegistration =
            projection.Subscribe(
                new ThrowingProjectionObserver());

        var recordingObserver =
            new RecordingProjectionObserver();

        using IDisposable recordingRegistration =
            projection.Subscribe(
                recordingObserver);

        inventory.Publish(
            CreateEntry(
                "endpoint-one"));

        Assert.Single(
            recordingObserver.Changes);
    }

    [Fact]
    public void RegistrationDisposal_StopsLaterProjectionChanges()
    {
        var inventory =
            new TestObservedInventory();

        using var projection =
            new RuntimeHostAttachmentProjection(
                inventory,
                inventory);

        var observer =
            new RecordingProjectionObserver();

        IDisposable registration =
            projection.Subscribe(
                observer);

        registration.Dispose();
        registration.Dispose();

        inventory.Publish(
            CreateEntry(
                "endpoint-one"));

        Assert.Empty(
            observer.Changes);
    }

    [Fact]
    public void ProjectionDisposal_UnsubscribesWithoutDisposingInventory()
    {
        var inventory =
            new TestObservedInventory();

        var projection =
            new RuntimeHostAttachmentProjection(
                inventory,
                inventory);

        projection.Dispose();
        projection.Dispose();

        Assert.Equal(
            1,
            inventory.RegistrationDisposeCount);

        Assert.False(
            inventory.IsDisposed);

        inventory.Publish(
            CreateEntry(
                "endpoint-one"));
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

        public int RegistrationDisposeCount
        {
            get;
            private set;
        }

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

        public void NotifyStaleEnding(
            RuntimeEndpointAttachmentInventoryEntry entry,
            DateTimeOffset endedAtUtc)
        {
            _observer?.OnAttachmentEnded(
                new RuntimeEndpointAttachmentEnded(
                    entry,
                    endedAtUtc));
        }
    }

    private sealed class RecordingProjectionObserver
        : IRuntimeHostAttachmentProjectionObserver
    {
        public List<RuntimeHostAttachmentProjectionChange> Changes
        {
            get;
        } =
            [];

        public void OnAttachmentProjectionChanged(
            RuntimeHostAttachmentProjectionChange change)
        {
            Changes.Add(
                change);
        }
    }

    private sealed class ThrowingProjectionObserver
        : IRuntimeHostAttachmentProjectionObserver
    {
        public void OnAttachmentProjectionChanged(
            RuntimeHostAttachmentProjectionChange change)
        {
            throw new InvalidOperationException(
                "Projection observer failed.");
        }
    }

    private sealed class TestAttachmentSession
        : IEndpointAttachmentSession
    {
        public TestAttachmentSession(
            RuntimeEndpoint endpoint)
        {
            RuntimeEndpoint =
                endpoint;

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