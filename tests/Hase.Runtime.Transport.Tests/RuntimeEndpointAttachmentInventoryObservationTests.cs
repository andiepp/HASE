using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class RuntimeEndpointAttachmentInventoryObservationTests
{
    [Fact]
    public void Contract_ExposesDisposableObserverRegistration()
    {
        var method =
            typeof(IRuntimeEndpointAttachmentInventoryObservationSource)
                .GetMethod(
                    nameof(
                        IRuntimeEndpointAttachmentInventoryObservationSource
                            .Subscribe));

        Assert.NotNull(
            method);

        Assert.Equal(
            typeof(IDisposable),
            method.ReturnType);

        var observer =
            Assert.Single(
                method.GetParameters());

        Assert.Equal(
            typeof(IRuntimeEndpointAttachmentInventoryObserver),
            observer.ParameterType);
    }

    [Fact]
    public void PublishedContract_NullEntry_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeEndpointAttachmentPublished(
                null!));
    }

    [Fact]
    public void EndedContract_ValidatesEntryAndUtcTime()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeEndpointAttachmentEnded(
                null!,
                DateTimeOffset.UtcNow));

        Assert.Throws<ArgumentException>(
            () => new RuntimeEndpointAttachmentEnded(
                CreateEntry(
                    "endpoint-one"),
                new DateTimeOffset(
                    2026,
                    7,
                    24,
                    19,
                    0,
                    0,
                    TimeSpan.FromHours(
                        2))));
    }

    [Fact]
    public async Task AttachAsync_PublishesCommittedEntryOnce()
    {
        IEndpointAttachmentSession session =
            CreateSession(
                "endpoint-one");

        var inventory =
            new RuntimeEndpointAttachmentInventory(
                new QueueAttachmentService(
                    session));

        var observer =
            new RecordingObserver();

        using IDisposable registration =
            inventory.Subscribe(
                observer);

        RuntimeEndpointAttachmentInventoryEntry entry =
            await inventory.AttachAsync(
                CreateRequest());

        RuntimeEndpointAttachmentPublished publication =
            Assert.Single(
                observer.Publications);

        Assert.Same(
            entry,
            publication.Entry);

        Assert.Same(
            entry,
            Assert.Single(
                inventory.List()));

        await inventory.DisposeAsync();
    }

    [Fact]
    public async Task AttachAsync_FailedUnpublishedAttachment_DoesNotNotify()
    {
        var inventory =
            new RuntimeEndpointAttachmentInventory(
                new FailingAttachmentService());

        var observer =
            new RecordingObserver();

        using IDisposable registration =
            inventory.Subscribe(
                observer);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => inventory.AttachAsync(
                CreateRequest()));

        Assert.Empty(
            observer.Publications);

        Assert.Empty(
            observer.Endings);

        await inventory.DisposeAsync();
    }

    [Fact]
    public async Task AttachAsync_DuplicateIdentity_DoesNotPublishSecondEntry()
    {
        var inventory =
            new RuntimeEndpointAttachmentInventory(
                new QueueAttachmentService(
                    CreateSession(
                        "endpoint-one"),
                    CreateSession(
                        "endpoint-one")));

        var observer =
            new RecordingObserver();

        using IDisposable registration =
            inventory.Subscribe(
                observer);

        await inventory.AttachAsync(
            CreateRequest());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => inventory.AttachAsync(
                CreateRequest()));

        Assert.Single(
            observer.Publications);

        await inventory.DisposeAsync();
    }

    [Fact]
    public async Task DetachAsync_PublishesEndingForRemovedEntry()
    {
        var inventory =
            new RuntimeEndpointAttachmentInventory(
                new QueueAttachmentService(
                    CreateSession(
                        "endpoint-one")));

        var observer =
            new RecordingObserver();

        using IDisposable registration =
            inventory.Subscribe(
                observer);

        RuntimeEndpointAttachmentInventoryEntry entry =
            await inventory.AttachAsync(
                CreateRequest());

        DateTimeOffset beforeDetach =
            DateTimeOffset.UtcNow;

        bool detached =
            await inventory.DetachAsync(
                entry.EndpointId);

        DateTimeOffset afterDetach =
            DateTimeOffset.UtcNow;

        Assert.True(
            detached);

        RuntimeEndpointAttachmentEnded ending =
            Assert.Single(
                observer.Endings);

        Assert.Same(
            entry,
            ending.Entry);

        Assert.InRange(
            ending.EndedAtUtc,
            beforeDetach,
            afterDetach);

        Assert.Empty(
            inventory.List());

        await inventory.DisposeAsync();
    }

    [Fact]
    public async Task DetachAsync_MissingEntry_DoesNotNotify()
    {
        var inventory =
            new RuntimeEndpointAttachmentInventory(
                new QueueAttachmentService());

        var observer =
            new RecordingObserver();

        using IDisposable registration =
            inventory.Subscribe(
                observer);

        bool detached =
            await inventory.DetachAsync(
                new EndpointId(
                    "missing-endpoint"));

        Assert.False(
            detached);

        Assert.Empty(
            observer.Publications);

        Assert.Empty(
            observer.Endings);

        await inventory.DisposeAsync();
    }

    [Fact]
    public async Task RegistrationDisposal_StopsLaterNotifications()
    {
        var inventory =
            new RuntimeEndpointAttachmentInventory(
                new QueueAttachmentService(
                    CreateSession(
                        "endpoint-one")));

        var observer =
            new RecordingObserver();

        IDisposable registration =
            inventory.Subscribe(
                observer);

        registration.Dispose();
        registration.Dispose();

        await inventory.AttachAsync(
            CreateRequest());

        Assert.Empty(
            observer.Publications);

        Assert.Empty(
            observer.Endings);

        await inventory.DisposeAsync();
    }

    [Fact]
    public async Task ObserverFailure_DoesNotFailMutationOrLaterObserver()
    {
        var inventory =
            new RuntimeEndpointAttachmentInventory(
                new QueueAttachmentService(
                    CreateSession(
                        "endpoint-one")));

        using IDisposable failingRegistration =
            inventory.Subscribe(
                new ThrowingObserver());

        var recordingObserver =
            new RecordingObserver();

        using IDisposable recordingRegistration =
            inventory.Subscribe(
                recordingObserver);

        RuntimeEndpointAttachmentInventoryEntry entry =
            await inventory.AttachAsync(
                CreateRequest());

        Assert.Single(
            recordingObserver.Publications);

        Assert.True(
            await inventory.DetachAsync(
                entry.EndpointId));

        Assert.Single(
            recordingObserver.Endings);

        await inventory.DisposeAsync();
    }

    [Fact]
    public async Task InventoryDisposal_PublishesEndingForEveryEntry()
    {
        var inventory =
            new RuntimeEndpointAttachmentInventory(
                new QueueAttachmentService(
                    CreateSession(
                        "endpoint-one"),
                    CreateSession(
                        "endpoint-two")));

        var observer =
            new RecordingObserver();

        using IDisposable registration =
            inventory.Subscribe(
                observer);

        RuntimeEndpointAttachmentInventoryEntry firstEntry =
            await inventory.AttachAsync(
                CreateRequest());

        RuntimeEndpointAttachmentInventoryEntry secondEntry =
            await inventory.AttachAsync(
                CreateRequest());

        await inventory.DisposeAsync();

        Assert.Equal(
            2,
            observer.Endings.Count);

        Assert.Contains(
            observer.Endings,
            ending =>
                ReferenceEquals(
                    firstEntry,
                    ending.Entry));

        Assert.Contains(
            observer.Endings,
            ending =>
                ReferenceEquals(
                    secondEntry,
                    ending.Entry));
    }

    [Fact]
    public async Task Subscribe_AfterDisposal_Throws()
    {
        var inventory =
            new RuntimeEndpointAttachmentInventory(
                new QueueAttachmentService());

        await inventory.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(
            () => inventory.Subscribe(
                new RecordingObserver()));
    }

    private static RuntimeEndpointAttachmentInventoryEntry CreateEntry(
        string endpointId)
    {
        return new RuntimeEndpointAttachmentInventoryEntry(
            CreateSession(
                endpointId));
    }

    private static IEndpointAttachmentSession CreateSession(
        string endpointId)
    {
        EndpointAttachmentRequest request =
            CreateRequest();

        var endpoint =
            new RuntimeEndpoint(
                new RuntimeContext(),
                new EndpointDescriptor(
                    new EndpointId(
                        endpointId)));

        return new EndpointAttachmentSession(
            request,
            endpoint,
            Array.Empty<IAsyncDisposable>());
    }

    private static EndpointAttachmentRequest CreateRequest()
    {
        return new EndpointAttachmentRequest(
            new TestConnectionDefinition(),
            new TestDescriptorSource());
    }

    private sealed class QueueAttachmentService
        : IEndpointAttachmentService
    {
        private readonly Queue<IEndpointAttachmentSession>
            _sessions;

        public QueueAttachmentService(
            params IEndpointAttachmentSession[] sessions)
        {
            _sessions =
                new Queue<IEndpointAttachmentSession>(
                    sessions);
        }

        public Task<IEndpointAttachmentSession> AttachAsync(
            EndpointAttachmentRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                _sessions.Dequeue());
        }
    }

    private sealed class FailingAttachmentService
        : IEndpointAttachmentService
    {
        public Task<IEndpointAttachmentSession> AttachAsync(
            EndpointAttachmentRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromException<IEndpointAttachmentSession>(
                new InvalidOperationException(
                    "Attachment failed."));
        }
    }

    private sealed class RecordingObserver
        : IRuntimeEndpointAttachmentInventoryObserver
    {
        public List<RuntimeEndpointAttachmentPublished> Publications
        {
            get;
        } =
            [];

        public List<RuntimeEndpointAttachmentEnded> Endings
        {
            get;
        } =
            [];

        public void OnAttachmentPublished(
            RuntimeEndpointAttachmentPublished publication)
        {
            Publications.Add(
                publication);
        }

        public void OnAttachmentEnded(
            RuntimeEndpointAttachmentEnded ending)
        {
            Endings.Add(
                ending);
        }
    }

    private sealed class ThrowingObserver
        : IRuntimeEndpointAttachmentInventoryObserver
    {
        public void OnAttachmentPublished(
            RuntimeEndpointAttachmentPublished publication)
        {
            throw new InvalidOperationException(
                "Publication observer failed.");
        }

        public void OnAttachmentEnded(
            RuntimeEndpointAttachmentEnded ending)
        {
            throw new InvalidOperationException(
                "Ending observer failed.");
        }
    }

    private sealed class TestConnectionDefinition
        : IEndpointConnectionDefinition
    {
        public EndpointConnectionOrigin Origin =>
            EndpointConnectionOrigin.Configured;

        public EndpointId? ExpectedEndpointId =>
            null;
    }

    private sealed class TestDescriptorSource
        : IEndpointDescriptorSource
    {
    }
}