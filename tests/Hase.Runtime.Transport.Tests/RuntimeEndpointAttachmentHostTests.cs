using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class RuntimeEndpointAttachmentHostTests
{
    [Fact]
    public void Constructor_ShouldExposeRuntimeContextAndAttachmentInventory()
    {
        var runtimeContext =
            new RuntimeContext();

        var attachmentService =
            new TestEndpointAttachmentService();

        var host =
            new RuntimeEndpointAttachmentHost(
                runtimeContext,
                attachmentService);

        Assert.Same(
            runtimeContext,
            host.RuntimeContext);

        Assert.IsType<RuntimeEndpointAttachmentInventory>(
            host.AttachmentInventory);
    }

    [Fact]
    public async Task AttachmentInventory_ShouldAttachThroughSuppliedService()
    {
        var runtimeContext =
            new RuntimeContext();

        var session =
            new TestEndpointAttachmentSession(
                CreateRequest(),
                new RuntimeEndpoint(
                    runtimeContext,
                    new EndpointDescriptor(
                        new EndpointId(
                            "host-endpoint"))));

        var attachmentService =
            new TestEndpointAttachmentService(
                session);

        await using var host =
            new RuntimeEndpointAttachmentHost(
                runtimeContext,
                attachmentService);

        EndpointAttachmentRequest request =
            CreateRequest();

        RuntimeEndpointAttachmentInventoryEntry entry =
            await host.AttachmentInventory.AttachAsync(
                request);

        Assert.Same(
            request,
            attachmentService.LastRequest);

        Assert.Same(
            session,
            entry.AttachmentSession);

        Assert.Equal(
            session.RuntimeEndpoint.Descriptor.Id,
            entry.EndpointId);
    }

    [Fact]
    public async Task DisposeAsync_ShouldDisposeAttachmentInventory()
    {
        var runtimeContext =
            new RuntimeContext();

        var session =
            new TestEndpointAttachmentSession(
                CreateRequest(),
                new RuntimeEndpoint(
                    runtimeContext,
                    new EndpointDescriptor(
                        new EndpointId(
                            "host-endpoint"))));

        var host =
            new RuntimeEndpointAttachmentHost(
                runtimeContext,
                new TestEndpointAttachmentService(
                    session));

        await host.AttachmentInventory.AttachAsync(
            CreateRequest());

        await host.DisposeAsync();

        Assert.Equal(
            1,
            session.DisposeCallCount);

        Assert.Throws<ObjectDisposedException>(
            () => host.AttachmentInventory.List());

        await host.DisposeAsync();

        Assert.Equal(
            1,
            session.DisposeCallCount);
    }

    [Fact]
    public void Constructor_NullRuntimeContext_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeEndpointAttachmentHost(
                null!,
                new TestEndpointAttachmentService()));
    }

    [Fact]
    public void Constructor_NullAttachmentService_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeEndpointAttachmentHost(
                new RuntimeContext(),
                null!));
    }

    private static EndpointAttachmentRequest CreateRequest()
    {
        return new EndpointAttachmentRequest(
            new TestEndpointConnectionDefinition(),
            new TestEndpointDescriptorSource());
    }

    private sealed class TestEndpointAttachmentService
        : IEndpointAttachmentService
    {
        private readonly IEndpointAttachmentSession? _session;

        public TestEndpointAttachmentService(
            IEndpointAttachmentSession? session = null)
        {
            _session =
                session;
        }

        public EndpointAttachmentRequest? LastRequest
        {
            get;
            private set;
        }

        public Task<IEndpointAttachmentSession> AttachAsync(
            EndpointAttachmentRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            LastRequest =
                request;

            return Task.FromResult(
                _session
                ?? throw new InvalidOperationException(
                    "No test attachment session was configured."));
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