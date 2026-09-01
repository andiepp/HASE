using System.IO;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.DesktopHost.App.Hosting;
using Hase.Mcnf.RfLab;
using Hase.Runtime.Northbound;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Hase.Transport.Serial;
using Hase.DesktopHost.Hosting;
using Hase.Mcnf.RfLab.DesktopHost;

namespace Hase.DesktopHost.Tests;

public sealed class DesktopRuntimeHostRfLabAttachmentInventoryAdapterTests
{
    [Fact]
    public void ConnectionDefinition_ConfiguredValues_ShouldPreserveIdentityAndHideTarget()
    {
        const string sensitiveTarget = "sensitive-target";
        var definition = new DesktopRuntimeHostRfLabConnectionDefinition(
            new EndpointId("rflab-01"),
            new SerialTransportOptions(sensitiveTarget, 115200));

        Assert.Equal(EndpointConnectionOrigin.Configured, definition.Origin);
        Assert.Equal(new EndpointId("rflab-01"), definition.ExpectedEndpointId);
        Assert.Same(RfLabReadOnlyDefinition.EndpointDefinition, definition.Definition);
        Assert.Equal(sensitiveTarget, definition.SerialOptions.PortName);
        Assert.DoesNotContain(sensitiveTarget, definition.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AttachAsync_ValidRequest_ShouldExposeEndpointOperations()
    {
        var context = new RuntimeContext();
        var factory = new RecordingFactory(context);
        var service = new DesktopRuntimeHostRfLabAttachmentService(factory);
        EndpointAttachmentRequest request = Request("rflab-01", "target-one");

        await using IEndpointAttachmentSession session =
            await service.AttachAsync(request);

        Assert.Same(request, session.Request);
        Assert.Equal(new EndpointId("rflab-01"), session.RuntimeEndpoint.Descriptor.Id);
        Assert.Same(factory.PropertyOperations, session.PropertyOperations);
        Assert.Same(factory.CommandOperations, session.CommandOperations);
        Assert.Single(context.Endpoints);

        await session.ShutdownAsync();
        Assert.Empty(context.Endpoints);
        Assert.Equal(1, factory.DisposeCount);
    }

    [Fact]
    public async Task AttachAsync_ControlledDefinitionReachesFactoryAndEndpoint()
    {
        var context = new RuntimeContext();
        var factory = new RecordingFactory(context);
        var service = new DesktopRuntimeHostRfLabAttachmentService(factory);
        var request = new EndpointAttachmentRequest(
            new DesktopRuntimeHostRfLabConnectionDefinition(
                new EndpointId("rflab-01"),
                RfLabControlledSignalDefinition.EndpointDefinition,
                new SerialTransportOptions("external-target", 115200)),
            HostRepositoryDescriptorSource.Instance);

        await using IEndpointAttachmentSession session = await service.AttachAsync(request);

        Assert.Same(RfLabControlledSignalDefinition.EndpointDefinition, factory.Definition);
        Assert.Equal(17, session.RuntimeEndpoint.Instruments.Single().Properties.Count);
        Assert.Equal(11, session.RuntimeEndpoint.Instruments.Single().Commands.Count);
    }

    [Fact]
    public async Task AttachAsync_WrongConnectionFamily_ShouldRejectBeforeFactory()
    {
        var factory = new RecordingFactory(new RuntimeContext());
        var service = new DesktopRuntimeHostRfLabAttachmentService(factory);
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
        var service = new DesktopRuntimeHostRfLabAttachmentService(factory);
        var request = new EndpointAttachmentRequest(
            new DesktopRuntimeHostRfLabConnectionDefinition(
                new EndpointId("rflab-01"),
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
        var service = new DesktopRuntimeHostRfLabAttachmentService(factory);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AttachAsync(Request("rflab-01", sensitiveTarget)));

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
        var service = new DesktopRuntimeHostRfLabAttachmentService(factory);

        AggregateException exception = await Assert.ThrowsAsync<AggregateException>(
            () => service.AttachAsync(Request("rflab-01", sensitiveTarget)));

        Assert.Equal(2, exception.InnerExceptions.Count);
        Assert.Empty(context.Endpoints);
        Assert.DoesNotContain(sensitiveTarget, exception.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(AvailabilityFailures))]
    public async Task AttachAsync_AvailabilityFailure_ShouldPreserveSanitizedClassification(
        Exception failure,
        string expectedCategory)
    {
        const string sensitiveTarget = "sensitive-target";
        var factory = new RecordingFactory(
            new RuntimeContext(),
            openFailure: failure);
        var service = new DesktopRuntimeHostRfLabAttachmentService(factory);

        DesktopRuntimeHostEndpointUnavailableException exception =
            await Assert.ThrowsAsync<
                DesktopRuntimeHostEndpointUnavailableException>(
                () => service.AttachAsync(
                    Request("rflab-01", sensitiveTarget)));

        Assert.Equal(expectedCategory, exception.FailureCategory);
        Assert.Equal(1, factory.OpenCount);
        Assert.DoesNotContain(
            sensitiveTarget,
            exception.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task AttachAsync_Cancellation_ShouldPropagateWithoutTargetLeak()
    {
        const string sensitiveTarget = "sensitive-target";
        var factory = new RecordingFactory(
            new RuntimeContext(),
            cancelOpen: true,
            sensitiveTarget: sensitiveTarget);
        var service = new DesktopRuntimeHostRfLabAttachmentService(factory);

        OperationCanceledException exception =
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => service.AttachAsync(Request("rflab-01", sensitiveTarget)));

        Assert.DoesNotContain(sensitiveTarget, exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Inventory_AttachFindAndDetach_ShouldUseAuthoritativePath()
    {
        var context = new RuntimeContext();
        var factory = new RecordingFactory(context);
        var inventory = new RuntimeEndpointAttachmentInventory(
            new DesktopRuntimeHostRfLabAttachmentService(factory));

        RuntimeEndpointAttachmentInventoryEntry entry =
            await inventory.AttachAsync(Request("rflab-01", "target-one"));

        Assert.Same(entry, inventory.Find(new EndpointId("rflab-01")));
        Assert.Same(entry, Assert.Single(inventory.List()));
        Assert.Same(factory.PropertyOperations, entry.AttachmentSession.PropertyOperations);
        Assert.Same(factory.CommandOperations, entry.AttachmentSession.CommandOperations);

        Assert.True(await inventory.DetachAsync(new EndpointId("rflab-01")));
        Assert.Empty(inventory.List());
        Assert.Empty(context.Endpoints);
        Assert.Equal(1, factory.DisposeCount);

        await inventory.DisposeAsync();
    }

    [Fact]
    public async Task NorthboundCommandService_ApplyCarrierCommand_ShouldUseAttachmentPortOnce()
    {
        string directoryPath = Path.Combine(
            Path.GetTempPath(),
            $"hase-rflab-command-composition-{Guid.NewGuid():N}");

        try
        {
            var context = new RuntimeContext();
            var factory = new RecordingFactory(context);
            await using var inventory = new RuntimeEndpointAttachmentInventory(
                new DesktopRuntimeHostRfLabAttachmentService(factory));
            var request = new EndpointAttachmentRequest(
                new DesktopRuntimeHostRfLabConnectionDefinition(
                    new EndpointId("rflab-01"),
                    RfLabControlledSignalDefinition.EndpointDefinition,
                    new SerialTransportOptions("external-target", 115200)),
                HostRepositoryDescriptorSource.Instance);

            RuntimeEndpointAttachmentInventoryEntry entry =
                await inventory.AttachAsync(request);
            await using RuntimeHostNorthboundSnapshotComposition composition =
                await RuntimeHostNorthboundSnapshotComposition.CreateFileBackedAsync(
                    inventory,
                    Path.Combine(directoryPath, "runtime-host-identity.json"),
                    new RuntimeHostId("runtime-host-rflab-command-composition"));
            PublishedRuntimeEndpointSnapshot endpoint = Assert.Single(
                composition.InventorySnapshotProvider.List());
            InstrumentId instrumentId = entry.RuntimeEndpoint.Instruments
                .Single()
                .Descriptor
                .Id;
            var target = new RuntimeHostCommandTarget(
                endpoint.EndpointId,
                endpoint.Generation,
                instrumentId,
                RfLabCommandMapping.ApplyCarrier.CommandPath);

            RuntimeHostCommandOperationResult result =
                await composition.CommandService.ExecuteAsync(target, argument: null);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, factory.CommandOperations.ExecuteCount);
            Assert.Equal(instrumentId, factory.CommandOperations.InstrumentId);
            Assert.Equal(
                RfLabCommandMapping.ApplyCarrier.CommandPath,
                factory.CommandOperations.CommandPath);
            Assert.Null(factory.CommandOperations.Argument);
            Assert.Equal(0, factory.PropertyOperations.CallCount);
        }
        finally
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
    }

    public static IEnumerable<object[]> AvailabilityFailures()
    {
        const string sensitiveTarget = "sensitive-target";

        yield return
        [
            new SerialPortOpenException(
                sensitiveTarget,
                SerialPortOpenFailure.Busy,
                new IOException(sensitiveTarget)),
            "SerialPortBusy"
        ];
        yield return
        [
            new SerialPortOpenException(
                sensitiveTarget,
                SerialPortOpenFailure.AccessDenied,
                new UnauthorizedAccessException(sensitiveTarget)),
            "SerialPortAccessDenied"
        ];
        yield return
        [
            new TimeoutException(sensitiveTarget),
            "TimedOut"
        ];
        yield return
        [
            new IOException(sensitiveTarget),
            "IoUnavailable"
        ];
    }

    private static EndpointAttachmentRequest Request(
        string endpointId,
        string serialTarget) =>
        new(
            new DesktopRuntimeHostRfLabConnectionDefinition(
                new EndpointId(endpointId),
                new SerialTransportOptions(serialTarget, 115200)),
            HostRepositoryDescriptorSource.Instance);

    private sealed class RecordingFactory(
        RuntimeContext context,
        string? returnedEndpointId = null,
        bool cancelOpen = false,
        Exception? openFailure = null,
        bool failDisposal = false,
        string sensitiveTarget = "unused-sensitive-target")
        : IDesktopRuntimeHostRfLabAttachmentFactory
    {
        public int OpenCount { get; private set; }
        public int DisposeCount { get; private set; }
        public EndpointDescriptorDefinition? Definition { get; private set; }
        public RecordingPropertyOperations PropertyOperations { get; } = new();
        public RecordingCommandOperations CommandOperations { get; } = new();

        public Task<IDesktopRuntimeHostRfLabAttachment> OpenAsync(
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

            if (openFailure is not null)
            {
                throw openFailure;
            }

            var actualEndpointId = new EndpointId(returnedEndpointId ?? endpointId.Value);
            RuntimeEndpoint endpoint = context.CreateEndpoint(
                definition.Materialize(actualEndpointId));
            context.PublishEndpoint(endpoint);

            return Task.FromResult<IDesktopRuntimeHostRfLabAttachment>(
                new RecordingAttachment(
                    context,
                    endpoint,
                    PropertyOperations,
                    CommandOperations,
                    () => DisposeCount++,
                    failDisposal,
                    sensitiveTarget));
        }
    }

    private sealed class RecordingAttachment(
        RuntimeContext context,
        RuntimeEndpoint runtimeEndpoint,
        IEndpointAttachmentPropertyOperations propertyOperations,
        IEndpointAttachmentCommandOperations commandOperations,
        Action onDispose,
        bool failDisposal,
        string sensitiveTarget) : IDesktopRuntimeHostRfLabAttachment
    {
        private bool disposed;

        public RuntimeEndpoint RuntimeEndpoint => runtimeEndpoint;
        public IEndpointAttachmentPropertyOperations PropertyOperations => propertyOperations;
        public IEndpointAttachmentCommandOperations CommandOperations => commandOperations;

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

    internal sealed class RecordingPropertyOperations
        : IEndpointAttachmentPropertyOperations
    {
        public int CallCount { get; private set; }

        public Task<EndpointAttachmentPropertyOperationResult> ReadAsync(
            InstrumentId instrumentId,
            PropertyId propertyId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            throw new NotSupportedException();
        }

        public Task<EndpointAttachmentPropertyOperationResult> WriteAsync(
            InstrumentId instrumentId,
            PropertyId propertyId,
            object? requestedValue,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            throw new NotSupportedException();
        }
    }

    internal sealed class RecordingCommandOperations
        : IEndpointAttachmentCommandOperations
    {
        public int ExecuteCount { get; private set; }
        public InstrumentId? InstrumentId { get; private set; }
        public DescriptorPath? CommandPath { get; private set; }
        public object? Argument { get; private set; }

        public Task<EndpointAttachmentCommandOperationResult> ExecuteAsync(
            InstrumentId instrumentId,
            DescriptorPath commandPath,
            object? argument,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExecuteCount++;
            InstrumentId = instrumentId;
            CommandPath = commandPath;
            Argument = argument;
            return Task.FromResult(
                EndpointAttachmentCommandOperationResult.Successful());
        }
    }

    private sealed class UnsupportedConnectionDefinition : IEndpointConnectionDefinition
    {
        public EndpointConnectionOrigin Origin => EndpointConnectionOrigin.Configured;
        public EndpointId? ExpectedEndpointId => null;
    }
}
