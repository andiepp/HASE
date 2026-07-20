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
        string endpointId)
    {
        var runtimeEndpoint =
            new RuntimeEndpoint(
                new RuntimeContext(),
                new EndpointDescriptor(
                    new EndpointId(
                        endpointId)));

        return new TestEndpointAttachmentSession(
            CreateRequest(),
            runtimeEndpoint);
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

        public Task<IEndpointAttachmentSession> AttachAsync(
            EndpointAttachmentRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                _sessions.Dequeue());
        }
    }

    private sealed class TestEndpointAttachmentSession
        : IEndpointAttachmentSession
    {
        public TestEndpointAttachmentSession(
            EndpointAttachmentRequest request,
            RuntimeEndpoint runtimeEndpoint)
        {
            Request =
                request;

            RuntimeEndpoint =
                runtimeEndpoint;
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

            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCallCount++;

            return ValueTask.CompletedTask;
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