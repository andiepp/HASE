using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;
using Hase.DesktopHost.App.Hosting;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Hase.Scpi.Kel103;
using Hase.Transport.Serial;

namespace Hase.DesktopHost.Tests;

public sealed class DesktopRuntimeHostKel103AttachmentInventoryAdapterTests
{
    [Fact]
    public void ConnectionDefinition_ConfiguredValues_ShouldPreserveIdentityAndHideTarget()
    {
        const string sensitiveTarget = "sensitive-target";
        var definition = new DesktopRuntimeHostKel103ConnectionDefinition(
            new EndpointId("kel-01"),
            new SerialTransportOptions(sensitiveTarget, 115200));

        Assert.Equal(EndpointConnectionOrigin.Configured, definition.Origin);
        Assert.Equal(new EndpointId("kel-01"), definition.ExpectedEndpointId);
        Assert.Same(Kel103ReadOnlyMeasurementDefinition.EndpointDefinition, definition.Definition);
        Assert.Equal(sensitiveTarget, definition.SerialOptions.PortName);
        Assert.DoesNotContain(sensitiveTarget, definition.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AttachAsync_ValidRequest_ShouldExposeEndpointAndPropertyOperations()
    {
        var context = new RuntimeContext();
        var factory = new RecordingFactory(context);
        var service = new DesktopRuntimeHostKel103AttachmentService(factory);
        EndpointAttachmentRequest request = Request("kel-01", "target-one");

        await using IEndpointAttachmentSession session =
            await service.AttachAsync(request);

        Assert.Same(request, session.Request);
        Assert.Equal(new EndpointId("kel-01"), session.RuntimeEndpoint.Descriptor.Id);
        Assert.Same(factory.PropertyOperations, session.PropertyOperations);
        Assert.Single(context.Endpoints);

        await session.ShutdownAsync();
        Assert.Empty(context.Endpoints);
        Assert.Equal(1, factory.DisposeCount);
    }

    [Fact]
    public async Task AttachAsync_VersionFourDefinitionReachesFactoryAndEndpoint()
    {
        var context = new RuntimeContext();
        var factory = new RecordingFactory(context);
        var service = new DesktopRuntimeHostKel103AttachmentService(factory);
        var request = new EndpointAttachmentRequest(
            new DesktopRuntimeHostKel103ConnectionDefinition(
                new EndpointId("kel-01"),
                Kel103ControlledSetpointDefinition.EndpointDefinition,
                new SerialTransportOptions("external-target", 115200)),
            HostRepositoryDescriptorSource.Instance);

        await using IEndpointAttachmentSession session = await service.AttachAsync(request);

        Assert.Same(Kel103ControlledSetpointDefinition.EndpointDefinition, factory.Definition);
        Assert.Equal(11, session.RuntimeEndpoint.Instruments.Single().Properties.Count);
        Assert.Equal(5, session.RuntimeEndpoint.Instruments.Single().Commands.Count);
    }

    [Fact]
    public async Task AttachAsync_WrongConnectionFamily_ShouldRejectBeforeFactory()
    {
        var factory = new RecordingFactory(new RuntimeContext());
        var service = new DesktopRuntimeHostKel103AttachmentService(factory);
        var request = new EndpointAttachmentRequest(
            new UnsupportedConnectionDefinition(),
            HostRepositoryDescriptorSource.Instance);

        await Assert.ThrowsAsync<NotSupportedException>(
            () => service.AttachAsync(request));

        Assert.Equal(0, factory.OpenCount);
    }

    [Fact]
    public async Task AttachAsync_WrongDescriptorSource_ShouldRejectBeforeFactory()
    {
        var factory = new RecordingFactory(new RuntimeContext());
        var service = new DesktopRuntimeHostKel103AttachmentService(factory);
        var request = new EndpointAttachmentRequest(
            new DesktopRuntimeHostKel103ConnectionDefinition(
                new EndpointId("kel-01"),
                new SerialTransportOptions("target-one", 115200)),
            EndpointProvidedDescriptorSource.Instance);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.AttachAsync(request));

        Assert.Equal(0, factory.OpenCount);
    }

    [Fact]
    public async Task AttachAsync_MismatchedIdentity_ShouldDisposeCandidateAndHideTarget()
    {
        const string sensitiveTarget = "sensitive-target";
        var context = new RuntimeContext();
        var factory = new RecordingFactory(context, returnedEndpointId: "different");
        var service = new DesktopRuntimeHostKel103AttachmentService(factory);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AttachAsync(Request("kel-01", sensitiveTarget)));

        Assert.Empty(context.Endpoints);
        Assert.Equal(1, factory.DisposeCount);
        Assert.DoesNotContain(sensitiveTarget, exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AttachAsync_ValidationAndCleanupFailures_ShouldAggregateWithoutTargetLeak()
    {
        const string sensitiveTarget = "sensitive-target";
        var context = new RuntimeContext();
        var factory = new RecordingFactory(
            context,
            returnedEndpointId: "different",
            failDisposal: true,
            sensitiveTarget: sensitiveTarget);
        var service = new DesktopRuntimeHostKel103AttachmentService(factory);

        AggregateException exception = await Assert.ThrowsAsync<AggregateException>(
            () => service.AttachAsync(Request("kel-01", sensitiveTarget)));

        Assert.Equal(2, exception.InnerExceptions.Count);
        Assert.Empty(context.Endpoints);
        Assert.DoesNotContain(sensitiveTarget, exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Inventory_AttachFindObserveAndDetach_ShouldUseAuthoritativePath()
    {
        var context = new RuntimeContext();
        var factory = new RecordingFactory(context);
        var inventory = new RuntimeEndpointAttachmentInventory(
            new DesktopRuntimeHostKel103AttachmentService(factory));
        var observer = new RecordingObserver();
        using IDisposable subscription = inventory.Subscribe(observer);

        RuntimeEndpointAttachmentInventoryEntry entry =
            await inventory.AttachAsync(Request("kel-01", "target-one"));

        Assert.Same(entry, inventory.Find(new EndpointId("kel-01")));
        Assert.Same(entry, Assert.Single(inventory.List()));
        Assert.Same(entry, Assert.Single(observer.Published).Entry);
        Assert.Same(factory.PropertyOperations, entry.AttachmentSession.PropertyOperations);

        Assert.True(await inventory.DetachAsync(new EndpointId("kel-01")));
        Assert.Empty(inventory.List());
        Assert.Empty(context.Endpoints);
        Assert.Same(entry, Assert.Single(observer.Ended).Entry);
        Assert.Equal(1, factory.DisposeCount);

        await inventory.DisposeAsync();
    }

    [Fact]
    public async Task Inventory_DuplicateIdentity_ShouldPreserveExistingAttachment()
    {
        var context = new RuntimeContext();
        var factory = new RecordingFactory(context);
        var inventory = new RuntimeEndpointAttachmentInventory(
            new DesktopRuntimeHostKel103AttachmentService(factory));
        EndpointAttachmentRequest request = Request("kel-01", "target-one");

        RuntimeEndpointAttachmentInventoryEntry existing =
            await inventory.AttachAsync(request);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => inventory.AttachAsync(request));

        Assert.Same(existing, Assert.Single(inventory.List()));
        Assert.Single(context.Endpoints);

        await inventory.DisposeAsync();
        Assert.Empty(context.Endpoints);
    }

    [Fact]
    public async Task AttachAsync_Cancellation_ShouldPropagateWithoutTargetLeak()
    {
        const string sensitiveTarget = "sensitive-target";
        var factory = new RecordingFactory(
            new RuntimeContext(),
            cancelOpen: true,
            sensitiveTarget: sensitiveTarget);
        var service = new DesktopRuntimeHostKel103AttachmentService(factory);

        OperationCanceledException exception =
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => service.AttachAsync(Request("kel-01", sensitiveTarget)));

        Assert.DoesNotContain(sensitiveTarget, exception.ToString(), StringComparison.Ordinal);
    }

    private static EndpointAttachmentRequest Request(
        string endpointId,
        string serialTarget) =>
        new(
            new DesktopRuntimeHostKel103ConnectionDefinition(
                new EndpointId(endpointId),
                new SerialTransportOptions(serialTarget, 115200)),
            HostRepositoryDescriptorSource.Instance);

    private sealed class RecordingFactory(
        RuntimeContext context,
        string? returnedEndpointId = null,
        bool cancelOpen = false,
        bool failDisposal = false,
        string sensitiveTarget = "unused-sensitive-target")
        : IDesktopRuntimeHostKel103AttachmentFactory
    {
        public int OpenCount { get; private set; }
        public int DisposeCount { get; private set; }
        public EndpointDescriptorDefinition? Definition { get; private set; }
        public RecordingPropertyOperations PropertyOperations { get; } = new();

        public Task<IDesktopRuntimeHostKel103Attachment> OpenAsync(
            EndpointId endpointId,
            SerialTransportOptions serialOptions,
            CancellationToken cancellationToken = default)
            => OpenAsync(
                endpointId,
                Kel103ReadOnlyMeasurementDefinition.EndpointDefinition,
                serialOptions,
                cancellationToken);

        public Task<IDesktopRuntimeHostKel103Attachment> OpenAsync(
            EndpointId endpointId,
            EndpointDescriptorDefinition definition,
            SerialTransportOptions serialOptions,
            CancellationToken cancellationToken = default)
        {
            OpenCount++;
            Definition = definition;

            if (cancelOpen)
            {
                throw new OperationCanceledException(
                    $"Opening {sensitiveTarget} was cancelled.",
                    cancellationToken);
            }

            var actualEndpointId = new EndpointId(returnedEndpointId ?? endpointId.Value);
            RuntimeEndpoint endpoint = context.CreateEndpoint(
                definition.Materialize(actualEndpointId));
            context.PublishEndpoint(endpoint);

            return Task.FromResult<IDesktopRuntimeHostKel103Attachment>(
                new RecordingAttachment(
                    context,
                    endpoint,
                    PropertyOperations,
                    () => DisposeCount++,
                    failDisposal,
                    sensitiveTarget));
        }
    }

    private sealed class RecordingAttachment(
        RuntimeContext context,
        RuntimeEndpoint runtimeEndpoint,
        IEndpointAttachmentPropertyOperations propertyOperations,
        Action onDispose,
        bool failDisposal,
        string sensitiveTarget) : IDesktopRuntimeHostKel103Attachment
    {
        private bool disposed;

        public RuntimeEndpoint RuntimeEndpoint => runtimeEndpoint;
        public IEndpointAttachmentPropertyOperations PropertyOperations => propertyOperations;

        public ValueTask DisposeAsync()
        {
            if (!disposed)
            {
                disposed = true;
                context.RemoveEndpoint(runtimeEndpoint);
                onDispose();

                if (failDisposal)
                {
                    throw new InvalidOperationException(
                        $"Disposal for {sensitiveTarget} failed.");
                }
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingPropertyOperations
        : IEndpointAttachmentPropertyOperations
    {
        public Task<EndpointAttachmentPropertyOperationResult> ReadAsync(
            InstrumentId instrumentId,
            PropertyId propertyId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EndpointAttachmentPropertyOperationResult> WriteAsync(
            InstrumentId instrumentId,
            PropertyId propertyId,
            object? requestedValue,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingObserver : IRuntimeEndpointAttachmentInventoryObserver
    {
        public List<RuntimeEndpointAttachmentPublished> Published { get; } = [];
        public List<RuntimeEndpointAttachmentEnded> Ended { get; } = [];

        public void OnAttachmentPublished(RuntimeEndpointAttachmentPublished publication) =>
            Published.Add(publication);

        public void OnAttachmentEnded(RuntimeEndpointAttachmentEnded ending) =>
            Ended.Add(ending);
    }

    private sealed class UnsupportedConnectionDefinition : IEndpointConnectionDefinition
    {
        public EndpointConnectionOrigin Origin => EndpointConnectionOrigin.Configured;
        public EndpointId? ExpectedEndpointId => null;
    }
}
