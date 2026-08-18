using System.IO;
using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.DesktopHost.App.Hosting;
using Hase.Runtime.Diagnostics;
using Hase.Runtime.Northbound;
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
    public async Task AttachAsync_ValidRequest_ShouldExposeEndpointOperations()
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
        Assert.Same(factory.CommandOperations, session.CommandOperations);
        Assert.Single(context.Endpoints);

        await session.ShutdownAsync();
        Assert.Empty(context.Endpoints);
        Assert.Equal(1, factory.DisposeCount);
    }

    [Theory]
    [InlineData(4, 5)]
    [InlineData(5, 8)]
    public async Task AttachAsync_ControlledDefinitionReachesFactoryAndEndpoint(
        ushort version,
        int expectedCommands)
    {
        var context = new RuntimeContext();
        var factory = new RecordingFactory(context);
        var service = new DesktopRuntimeHostKel103AttachmentService(factory);
        EndpointDescriptorDefinition definition = version == 4
            ? Kel103ControlledSetpointDefinition.EndpointDefinition
            : Kel103ControlledInputDefinition.EndpointDefinition;
        var request = new EndpointAttachmentRequest(
            new DesktopRuntimeHostKel103ConnectionDefinition(
                new EndpointId("kel-01"),
                definition,
                new SerialTransportOptions("external-target", 115200)),
            HostRepositoryDescriptorSource.Instance);

        await using IEndpointAttachmentSession session = await service.AttachAsync(request);

        Assert.Same(definition, factory.Definition);
        Assert.Equal(11, session.RuntimeEndpoint.Instruments.Single().Properties.Count);
        Assert.Equal(expectedCommands, session.RuntimeEndpoint.Instruments.Single().Commands.Count);
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
        var service = new DesktopRuntimeHostKel103AttachmentService(factory);

        DesktopRuntimeHostEndpointUnavailableException exception =
            await Assert.ThrowsAsync<
                DesktopRuntimeHostEndpointUnavailableException>(
                () => service.AttachAsync(
                    Request("kel-01", sensitiveTarget)));

        Assert.Equal(expectedCategory, exception.FailureCategory);
        Assert.Equal(1, factory.OpenCount);
        Assert.DoesNotContain(
            sensitiveTarget,
            exception.ToString(),
            StringComparison.Ordinal);
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
        Assert.Same(factory.CommandOperations, entry.AttachmentSession.CommandOperations);

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
    public async Task NorthboundCommandService_VersionFourModeCommand_ShouldUseAttachmentPortOnce()
    {
        string directoryPath = Path.Combine(
            Path.GetTempPath(),
            $"hase-kel103-command-composition-{Guid.NewGuid():N}");

        try
        {
            var context = new RuntimeContext();
            var factory = new RecordingFactory(context);
            await using var inventory = new RuntimeEndpointAttachmentInventory(
                new DesktopRuntimeHostKel103AttachmentService(factory));
            var request = new EndpointAttachmentRequest(
                new DesktopRuntimeHostKel103ConnectionDefinition(
                    new EndpointId("kel-01"),
                    Kel103ControlledSetpointDefinition.EndpointDefinition,
                    new SerialTransportOptions("external-target", 115200)),
                HostRepositoryDescriptorSource.Instance);

            RuntimeEndpointAttachmentInventoryEntry entry =
                await inventory.AttachAsync(request);
            await using RuntimeHostNorthboundSnapshotComposition composition =
                await RuntimeHostNorthboundSnapshotComposition.CreateFileBackedAsync(
                    inventory,
                    Path.Combine(directoryPath, "runtime-host-identity.json"),
                    new RuntimeHostId("runtime-host-kel103-command-composition"));
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
                Kel103ModeSelectionMapping.ConstantVoltage.CommandPath);

            RuntimeHostCommandOperationResult result =
                await composition.CommandService.ExecuteAsync(target, argument: null);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, factory.CommandOperations.ExecuteCount);
            Assert.Equal(instrumentId, factory.CommandOperations.InstrumentId);
            Assert.Equal(
                Kel103ModeSelectionMapping.ConstantVoltage.CommandPath,
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

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task NorthboundCommandService_VersionFiveInputCommandPreservesInventoryAndDiagnostics(
        int mappingIndex)
    {
        string directoryPath = Path.Combine(
            Path.GetTempPath(),
            $"hase-kel103-input-command-composition-{Guid.NewGuid():N}");

        try
        {
            var collector = new BoundedRuntimeDiagnosticCollector(10);
            var diagnostics = new RuntimeDiagnosticPublisher(collector);
            var context = new RuntimeContext(diagnostics);
            var factory = new RecordingFactory(context);
            await using var inventory = new RuntimeEndpointAttachmentInventory(
                new DesktopRuntimeHostKel103AttachmentService(factory));
            var request = new EndpointAttachmentRequest(
                new DesktopRuntimeHostKel103ConnectionDefinition(
                    new EndpointId("kel-01"),
                    Kel103ControlledInputDefinition.EndpointDefinition,
                    new SerialTransportOptions("external-target", 115200)),
                HostRepositoryDescriptorSource.Instance);

            RuntimeEndpointAttachmentInventoryEntry entry =
                await inventory.AttachAsync(request);
            await using RuntimeHostNorthboundSnapshotComposition composition =
                await RuntimeHostNorthboundSnapshotComposition.CreateFileBackedAsync(
                    inventory,
                    Path.Combine(directoryPath, "runtime-host-identity.json"),
                    new RuntimeHostId("runtime-host-kel103-input-command-composition"),
                    diagnostics: diagnostics);
            PublishedRuntimeEndpointSnapshot endpoint = Assert.Single(
                composition.InventorySnapshotProvider.List());
            CommandDescriptor[] commands = endpoint.Descriptor.Instruments
                .Single()
                .Interface.Commands
                .ToArray();
            Kel103InputControlMapping mapping = Kel103InputControlMapping.All[mappingIndex];
            InstrumentId instrumentId = entry.RuntimeEndpoint.Instruments
                .Single()
                .Descriptor.Id;
            var target = new RuntimeHostCommandTarget(
                endpoint.EndpointId,
                endpoint.Generation,
                instrumentId,
                mapping.CommandPath);
            object? argument = mapping.RequiresConfirmation ? true : null;

            RuntimeHostCommandOperationResult result =
                await composition.CommandService.ExecuteAsync(target, argument);

            Assert.True(result.IsSuccess);
            Assert.Equal(8, commands.Length);
            CommandDescriptor shortActivation = commands.Single(
                command => command.Path == Kel103InputControlMapping.ShortCircuitActivate.CommandPath);
            Assert.IsType<BooleanDataDescriptor>(shortActivation.Argument!.Data);
            Assert.Equal(1, factory.CommandOperations.ExecuteCount);
            Assert.Equal(instrumentId, factory.CommandOperations.InstrumentId);
            Assert.Equal(mapping.CommandPath, factory.CommandOperations.CommandPath);
            Assert.Equal(argument, factory.CommandOperations.Argument);
            Assert.Equal(0, factory.PropertyOperations.CallCount);

            IReadOnlyList<RuntimeDiagnosticRecord> records = collector.GetSnapshot()
                .Where(record => record.EventName.StartsWith(
                    "CommandExecution",
                    StringComparison.Ordinal))
                .ToArray();
            Assert.Equal(
                ["CommandExecutionStarted", "CommandExecutionCompleted"],
                records.Select(record => record.EventName).ToArray());
            Assert.Equal(records[0].OperationId, records[1].OperationId);
            Assert.Equal(endpoint.EndpointId.Value, records[0].EndpointId);
            Assert.Equal(endpoint.Generation.Value, records[0].AttachmentGeneration);
            Assert.Equal(instrumentId.Value, records[0].Details["instrument"]);
            Assert.Equal(mapping.CommandPath.ToString(), records[0].Details["path"]);
            Assert.All(records, record =>
            {
                Assert.Equal(2, record.Details.Count);
                Assert.DoesNotContain(
                    record.Details.Values,
                    value => value.Contains("True", StringComparison.OrdinalIgnoreCase));
            });
        }
        finally
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
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
                SerialPortOpenFailure.Unavailable,
                new IOException(sensitiveTarget)),
            "SerialPortUnavailable"
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
            new SerialPortOpenException(
                sensitiveTarget,
                SerialPortOpenFailure.Failed,
                new IOException(sensitiveTarget)),
            "SerialPortOpenFailed"
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
            new DesktopRuntimeHostKel103ConnectionDefinition(
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
        : IDesktopRuntimeHostKel103AttachmentFactory
    {
        public int OpenCount { get; private set; }
        public int DisposeCount { get; private set; }
        public EndpointDescriptorDefinition? Definition { get; private set; }
        public RecordingPropertyOperations PropertyOperations { get; } = new();
        public RecordingCommandOperations CommandOperations { get; } = new();

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

            if (openFailure is not null)
            {
                throw openFailure;
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
        string sensitiveTarget) : IDesktopRuntimeHostKel103Attachment
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

    private sealed class RecordingPropertyOperations
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

    private sealed class RecordingCommandOperations
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
