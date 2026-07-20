using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class RuntimeEndpointAttachmentInventoryTests
{
    [Fact]
    public async Task AttachAsync_ShouldAddAuthoritativeEndpointEntry()
    {
        TestEndpointAttachmentSession session =
            CreateSession(
                "endpoint-one");

        var inventory =
            new RuntimeEndpointAttachmentInventory(
                new QueueEndpointAttachmentService(
                    session));

        RuntimeEndpointAttachmentInventoryEntry entry =
            await inventory.AttachAsync(
                CreateRequest());

        Assert.Equal(
            session.RuntimeEndpoint.Descriptor.Id,
            entry.EndpointId);

        Assert.Same(
            entry,
            inventory.Find(
                entry.EndpointId));

        Assert.Same(
            entry,
            Assert.Single(
                inventory.List()));

        await inventory.DisposeAsync();
    }

    [Fact]
    public async Task List_ShouldReturnSnapshot()
    {
        TestEndpointAttachmentSession firstSession =
            CreateSession(
                "endpoint-one");

        TestEndpointAttachmentSession secondSession =
            CreateSession(
                "endpoint-two");

        var inventory =
            new RuntimeEndpointAttachmentInventory(
                new QueueEndpointAttachmentService(
                    firstSession,
                    secondSession));

        await inventory.AttachAsync(
            CreateRequest());

        IReadOnlyList<RuntimeEndpointAttachmentInventoryEntry> snapshot =
            inventory.List();

        await inventory.AttachAsync(
            CreateRequest());

        Assert.Single(
            snapshot);

        Assert.Equal(
            2,
            inventory.List().Count);

        await inventory.DisposeAsync();
    }

    [Fact]
    public async Task AttachAsync_DuplicateAuthoritativeIdentity_ShouldRejectAndDisposeNewSession()
    {
        TestEndpointAttachmentSession existingSession =
            CreateSession(
                "duplicate-endpoint");

        TestEndpointAttachmentSession duplicateSession =
            CreateSession(
                "duplicate-endpoint");

        var inventory =
            new RuntimeEndpointAttachmentInventory(
                new QueueEndpointAttachmentService(
                    existingSession,
                    duplicateSession));

        RuntimeEndpointAttachmentInventoryEntry existingEntry =
            await inventory.AttachAsync(
                CreateRequest());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => inventory.AttachAsync(
                CreateRequest()));

        Assert.Same(
            existingEntry,
            Assert.Single(
                inventory.List()));

        Assert.Equal(
            0,
            existingSession.DisposeCallCount);

        Assert.Equal(
            1,
            duplicateSession.DisposeCallCount);

        await inventory.DisposeAsync();
    }

    [Fact]
    public async Task AttachAsync_DuplicateCleanupFailure_ShouldReportBothFailures()
    {
        TestEndpointAttachmentSession existingSession =
            CreateSession(
                "duplicate-endpoint");

        var cleanupFailure =
            new InvalidOperationException(
                "Duplicate cleanup failed.");

        TestEndpointAttachmentSession duplicateSession =
            CreateSession(
                "duplicate-endpoint",
                disposeFailure: cleanupFailure);

        var inventory =
            new RuntimeEndpointAttachmentInventory(
                new QueueEndpointAttachmentService(
                    existingSession,
                    duplicateSession));

        RuntimeEndpointAttachmentInventoryEntry existingEntry =
            await inventory.AttachAsync(
                CreateRequest());

        AggregateException exception =
            await Assert.ThrowsAsync<AggregateException>(
                () => inventory.AttachAsync(
                    CreateRequest()));

        Assert.Equal(
            2,
            exception.InnerExceptions.Count);

        Assert.IsType<InvalidOperationException>(
            exception.InnerExceptions[0]);

        Assert.Same(
            cleanupFailure,
            exception.InnerExceptions[1]);

        Assert.Same(
            existingEntry,
            Assert.Single(
                inventory.List()));

        Assert.Equal(
            1,
            duplicateSession.DisposeCallCount);

        await inventory.DisposeAsync();
    }

    [Fact]
    public async Task DetachAsync_ExistingIdentity_ShouldRemoveAndShutdownSession()
    {
        TestEndpointAttachmentSession session =
            CreateSession(
                "endpoint-one");

        var inventory =
            new RuntimeEndpointAttachmentInventory(
                new QueueEndpointAttachmentService(
                    session));

        RuntimeEndpointAttachmentInventoryEntry entry =
            await inventory.AttachAsync(
                CreateRequest());

        bool detached =
            await inventory.DetachAsync(
                entry.EndpointId);

        Assert.True(
            detached);

        Assert.Equal(
            1,
            session.ShutdownCallCount);

        Assert.Empty(
            inventory.List());

        await inventory.DisposeAsync();
    }

    [Fact]
    public async Task DetachAsync_MissingIdentity_ShouldReturnFalse()
    {
        var inventory =
            new RuntimeEndpointAttachmentInventory(
                new QueueEndpointAttachmentService());

        bool detached =
            await inventory.DetachAsync(
                new EndpointId(
                    "missing-endpoint"));

        Assert.False(
            detached);

        await inventory.DisposeAsync();
    }

    [Fact]
    public async Task DetachAsync_ShutdownFailure_ShouldRemoveFailedEntryAndPreserveOthers()
    {
        var shutdownFailure =
            new InvalidOperationException(
                "Shutdown failed.");

        TestEndpointAttachmentSession failingSession =
            CreateSession(
                "failing-endpoint",
                shutdownFailure: shutdownFailure);

        TestEndpointAttachmentSession unaffectedSession =
            CreateSession(
                "unaffected-endpoint");

        var inventory =
            new RuntimeEndpointAttachmentInventory(
                new QueueEndpointAttachmentService(
                    failingSession,
                    unaffectedSession));

        RuntimeEndpointAttachmentInventoryEntry failingEntry =
            await inventory.AttachAsync(
                CreateRequest());

        RuntimeEndpointAttachmentInventoryEntry unaffectedEntry =
            await inventory.AttachAsync(
                CreateRequest());

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => inventory.DetachAsync(
                    failingEntry.EndpointId));

        Assert.Same(
            shutdownFailure,
            exception);

        Assert.Null(
            inventory.Find(
                failingEntry.EndpointId));

        Assert.Same(
            unaffectedEntry,
            Assert.Single(
                inventory.List()));

        Assert.Equal(
            1,
            failingSession.ShutdownCallCount);

        await inventory.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_ShouldDisposeAllSessionsAndBeIdempotent()
    {
        TestEndpointAttachmentSession firstSession =
            CreateSession(
                "endpoint-one");

        TestEndpointAttachmentSession secondSession =
            CreateSession(
                "endpoint-two");

        var inventory =
            new RuntimeEndpointAttachmentInventory(
                new QueueEndpointAttachmentService(
                    firstSession,
                    secondSession));

        await inventory.AttachAsync(
            CreateRequest());

        await inventory.AttachAsync(
            CreateRequest());

        await inventory.DisposeAsync();
        await inventory.DisposeAsync();

        Assert.Equal(
            1,
            firstSession.DisposeCallCount);

        Assert.Equal(
            1,
            secondSession.DisposeCallCount);
    }

    [Fact]
    public async Task DisposeAsync_SingleFailure_ShouldContinueAndRethrowFailure()
    {
        var disposeFailure =
            new InvalidOperationException(
                "Disposal failed.");

        TestEndpointAttachmentSession failingSession =
            CreateSession(
                "failing-endpoint",
                disposeFailure: disposeFailure);

        TestEndpointAttachmentSession succeedingSession =
            CreateSession(
                "succeeding-endpoint");

        var inventory =
            new RuntimeEndpointAttachmentInventory(
                new QueueEndpointAttachmentService(
                    failingSession,
                    succeedingSession));

        await inventory.AttachAsync(
            CreateRequest());

        await inventory.AttachAsync(
            CreateRequest());

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await inventory.DisposeAsync());

        Assert.Same(
            disposeFailure,
            exception);

        Assert.Equal(
            1,
            failingSession.DisposeCallCount);

        Assert.Equal(
            1,
            succeedingSession.DisposeCallCount);

        await inventory.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_MultipleFailures_ShouldAggregateFailures()
    {
        var firstFailure =
            new InvalidOperationException(
                "First disposal failed.");

        var secondFailure =
            new NotSupportedException(
                "Second disposal failed.");

        TestEndpointAttachmentSession firstSession =
            CreateSession(
                "endpoint-one",
                disposeFailure: firstFailure);

        TestEndpointAttachmentSession secondSession =
            CreateSession(
                "endpoint-two",
                disposeFailure: secondFailure);

        var inventory =
            new RuntimeEndpointAttachmentInventory(
                new QueueEndpointAttachmentService(
                    firstSession,
                    secondSession));

        await inventory.AttachAsync(
            CreateRequest());

        await inventory.AttachAsync(
            CreateRequest());

        AggregateException exception =
            await Assert.ThrowsAsync<AggregateException>(
                async () => await inventory.DisposeAsync());

        Assert.Collection(
            exception.InnerExceptions,
            failure => Assert.Same(
                firstFailure,
                failure),
            failure => Assert.Same(
                secondFailure,
                failure));

        Assert.Equal(
            1,
            firstSession.DisposeCallCount);

        Assert.Equal(
            1,
            secondSession.DisposeCallCount);

        await inventory.DisposeAsync();
    }

    [Fact]
    public async Task ConcurrentDuplicateAttachments_ShouldSerializeAndPreserveFirstEntry()
    {
        TestEndpointAttachmentSession firstSession =
            CreateSession(
                "duplicate-endpoint");

        TestEndpointAttachmentSession secondSession =
            CreateSession(
                "duplicate-endpoint");

        var firstAttachmentStarted =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var releaseFirstAttachment =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        int attachmentCallCount =
            0;

        var attachmentService =
            new DelegateEndpointAttachmentService(
                async cancellationToken =>
                {
                    int callNumber =
                        Interlocked.Increment(
                            ref attachmentCallCount);

                    if (callNumber == 1)
                    {
                        firstAttachmentStarted.TrySetResult();

                        await releaseFirstAttachment.Task.WaitAsync(
                            cancellationToken);

                        return firstSession;
                    }

                    return secondSession;
                });

        var inventory =
            new RuntimeEndpointAttachmentInventory(
                attachmentService);

        Task<RuntimeEndpointAttachmentInventoryEntry> firstAttachment =
            inventory.AttachAsync(
                CreateRequest());

        await firstAttachmentStarted.Task;

        Task<RuntimeEndpointAttachmentInventoryEntry> secondAttachment =
            inventory.AttachAsync(
                CreateRequest());

        Assert.Equal(
            1,
            Volatile.Read(
                ref attachmentCallCount));

        releaseFirstAttachment.TrySetResult();

        RuntimeEndpointAttachmentInventoryEntry firstEntry =
            await firstAttachment;

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await secondAttachment);

        Assert.Same(
            firstEntry,
            Assert.Single(
                inventory.List()));

        Assert.Equal(
            1,
            secondSession.DisposeCallCount);

        await inventory.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_DuringAttachment_ShouldWaitAndDisposeCompletedAttachment()
    {
        TestEndpointAttachmentSession session =
            CreateSession(
                "endpoint-one");

        var attachmentStarted =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var releaseAttachment =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var attachmentService =
            new DelegateEndpointAttachmentService(
                async cancellationToken =>
                {
                    attachmentStarted.TrySetResult();

                    await releaseAttachment.Task.WaitAsync(
                        cancellationToken);

                    return session;
                });

        var inventory =
            new RuntimeEndpointAttachmentInventory(
                attachmentService);

        Task<RuntimeEndpointAttachmentInventoryEntry> attachmentTask =
            inventory.AttachAsync(
                CreateRequest());

        await attachmentStarted.Task;

        Task disposalTask =
            inventory.DisposeAsync().AsTask();

        Assert.False(
            disposalTask.IsCompleted);

        releaseAttachment.TrySetResult();

        await attachmentTask;
        await disposalTask;

        Assert.Equal(
            1,
            session.DisposeCallCount);
    }

    [Fact]
    public async Task AttachAsync_QueuedBehindDisposal_ShouldRejectWithoutCallingService()
    {
        var disposalStarted =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var releaseDisposal =
            new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

        TestEndpointAttachmentSession existingSession =
            CreateSession(
                "existing-endpoint",
                disposalStarted: disposalStarted,
                releaseDisposal: releaseDisposal.Task);

        TestEndpointAttachmentSession unusedSession =
            CreateSession(
                "unused-endpoint");

        var attachmentService =
            new QueueEndpointAttachmentService(
                existingSession,
                unusedSession);

        var inventory =
            new RuntimeEndpointAttachmentInventory(
                attachmentService);

        await inventory.AttachAsync(
            CreateRequest());

        Task disposalTask =
            inventory.DisposeAsync().AsTask();

        await disposalStarted.Task;

        Task<RuntimeEndpointAttachmentInventoryEntry> attachmentTask =
            inventory.AttachAsync(
                CreateRequest());

        Assert.Equal(
            1,
            attachmentService.CallCount);

        releaseDisposal.TrySetResult();

        await disposalTask;

        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await attachmentTask);

        Assert.Equal(
            1,
            attachmentService.CallCount);

        Assert.Equal(
            0,
            unusedSession.DisposeCallCount);
    }

    [Fact]
    public async Task OperationsAfterDisposal_ShouldReject()
    {
        var inventory =
            new RuntimeEndpointAttachmentInventory(
                new QueueEndpointAttachmentService());

        await inventory.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => inventory.AttachAsync(
                CreateRequest()));

        Assert.Throws<ObjectDisposedException>(
            () => inventory.Find(
                new EndpointId(
                    "endpoint-one")));

        Assert.Throws<ObjectDisposedException>(
            () => inventory.List());

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => inventory.DetachAsync(
                new EndpointId(
                    "endpoint-one")));
    }

    private static TestEndpointAttachmentSession CreateSession(
        string endpointId,
        Exception? shutdownFailure = null,
        Exception? disposeFailure = null,
        TaskCompletionSource? disposalStarted = null,
        Task? releaseDisposal = null)
    {
        var runtimeEndpoint =
            new RuntimeEndpoint(
                new RuntimeContext(),
                new EndpointDescriptor(
                    new EndpointId(
                        endpointId)));

        return new TestEndpointAttachmentSession(
            CreateRequest(),
            runtimeEndpoint,
            shutdownFailure,
            disposeFailure,
            disposalStarted,
            releaseDisposal);
    }

    private static EndpointAttachmentRequest CreateRequest()
    {
        return new EndpointAttachmentRequest(
            new TestEndpointConnectionDefinition(),
            new TestEndpointDescriptorSource());
    }

    private sealed class QueueEndpointAttachmentService
        : IEndpointAttachmentService
    {
        private readonly Queue<IEndpointAttachmentSession>
            _sessions;

        public QueueEndpointAttachmentService(
            params IEndpointAttachmentSession[] sessions)
        {
            _sessions =
                new Queue<IEndpointAttachmentSession>(
                    sessions);
        }

        public int CallCount
        {
            get;
            private set;
        }

        public Task<IEndpointAttachmentSession> AttachAsync(
            EndpointAttachmentRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CallCount++;

            return Task.FromResult(
                _sessions.Dequeue());
        }
    }

    private sealed class DelegateEndpointAttachmentService
        : IEndpointAttachmentService
    {
        private readonly Func<
            CancellationToken,
            Task<IEndpointAttachmentSession>>
            _attachAsync;

        public DelegateEndpointAttachmentService(
            Func<
                CancellationToken,
                Task<IEndpointAttachmentSession>>
                attachAsync)
        {
            _attachAsync =
                attachAsync;
        }

        public Task<IEndpointAttachmentSession> AttachAsync(
            EndpointAttachmentRequest request,
            CancellationToken cancellationToken = default)
        {
            return _attachAsync(
                cancellationToken);
        }
    }

    private sealed class TestEndpointAttachmentSession
        : IEndpointAttachmentSession
    {
        public TestEndpointAttachmentSession(
            EndpointAttachmentRequest request,
            RuntimeEndpoint runtimeEndpoint,
            Exception? shutdownFailure,
            Exception? disposeFailure,
            TaskCompletionSource? disposalStarted,
            Task? releaseDisposal)
        {
            Request =
                request;

            RuntimeEndpoint =
                runtimeEndpoint;

            ShutdownFailure =
                shutdownFailure;

            DisposeFailure =
                disposeFailure;

            DisposalStarted =
                disposalStarted;

            ReleaseDisposal =
                releaseDisposal;
        }

        public int ShutdownCallCount
        {
            get;
            private set;
        }

        public int DisposeCallCount
        {
            get;
            private set;
        }

        private Exception? ShutdownFailure
        {
            get;
        }

        private Exception? DisposeFailure
        {
            get;
        }

        private TaskCompletionSource? DisposalStarted
        {
            get;
        }

        private Task? ReleaseDisposal
        {
            get;
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
            cancellationToken.ThrowIfCancellationRequested();

            ShutdownCallCount++;

            if (ShutdownFailure is not null)
            {
                return Task.FromException(
                    ShutdownFailure);
            }

            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            DisposeCallCount++;

            DisposalStarted?.TrySetResult();

            if (ReleaseDisposal is not null)
            {
                await ReleaseDisposal;
            }

            if (DisposeFailure is not null)
            {
                throw DisposeFailure;
            }
        }
    }

    private sealed class TestEndpointConnectionDefinition
        : IEndpointConnectionDefinition
    {
        public EndpointConnectionOrigin Origin =>
            EndpointConnectionOrigin.Configured;

        public EndpointId? ExpectedEndpointId =>
            null;
    }

    private sealed class TestEndpointDescriptorSource
        : IEndpointDescriptorSource
    {
    }
}