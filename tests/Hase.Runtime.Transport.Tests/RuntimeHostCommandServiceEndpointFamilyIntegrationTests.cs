using Hase.CompactProtocol;
using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Protocol;
using Hase.Runtime.Connections;
using Hase.Runtime.Northbound;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class RuntimeHostCommandServiceEndpointFamilyIntegrationTests
{
    private static readonly InstrumentId InstrumentId =
        new(
            "controller-one");

    private static readonly PropertyId PropertyId =
        new(
            "controller.state");

    private static readonly DescriptorPath CommandPath =
        new(
            "Controller",
            "ToggleLed");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommandService_NativeAndCompact_UseSameContract(
        bool useCompactEndpoint)
    {
        EndpointDescriptorDefinition descriptorDefinition =
            CreateDescriptorDefinition();

        var endpointId =
            new EndpointId(
                useCompactEndpoint
                    ? "compact-command-endpoint"
                    : "native-command-endpoint");

        RuntimeEndpoint runtimeEndpoint =
            new RuntimeContext()
                .AddEndpoint(
                    descriptorDefinition.Materialize(
                        endpointId));

        runtimeEndpoint.UpdateConnectionStatus(
            new EndpointConnectionStatus(
                EndpointConnectionState.Ready));

        RuntimeProperty runtimeProperty =
            runtimeEndpoint
                .FindInstrument(
                    InstrumentId)!
                .FindProperty(
                    PropertyId)!;

        PropertyValue originalValue =
            new(
                false,
                DateTimeOffset.UnixEpoch);

        runtimeProperty.UpdateValue(
            originalValue);

        object? expectedArgument =
            useCompactEndpoint
                ? null
                : "native-argument";

        object? expectedReturnValue =
            useCompactEndpoint
                ? null
                : "native-return-value";

        int executeCallCount =
            0;

        IEndpointAttachmentCommandOperations commandOperations =
            useCompactEndpoint
                ? CreateCompactOperations(
                    descriptorDefinition,
                    () =>
                        executeCallCount++)
                : CreateNativeOperations(
                    runtimeEndpoint,
                    expectedArgument,
                    expectedReturnValue,
                    () =>
                        executeCallCount++);

        var session =
            new EndpointAttachmentSession(
                new EndpointAttachmentRequest(
                    new StubConnectionDefinition(
                        endpointId),
                    HostRepositoryDescriptorSource.Instance),
                runtimeEndpoint,
                UnavailableEndpointAttachmentPropertyOperations.Instance,
                commandOperations,
                Array.Empty<IAsyncDisposable>());

        RuntimeHostNorthboundSnapshotComposition composition =
            await RuntimeHostNorthboundSnapshotComposition
                .CreateFileBackedAsync(
                    new TestAttachmentInventory(
                        new RuntimeEndpointAttachmentInventoryEntry(
                            session)),
                    Path.Combine(
                        Path.GetTempPath(),
                        $"hase-command-integration-{Guid.NewGuid():N}",
                        "runtime-host-identity.json"),
                    new RuntimeHostId(
                        "runtime-host-command-integration"));

        PublishedRuntimeEndpointSnapshot endpointSnapshot =
            Assert.Single(
                composition.InventorySnapshotProvider.List());

        var target =
            new RuntimeHostCommandTarget(
                endpointSnapshot.EndpointId,
                endpointSnapshot.Generation,
                InstrumentId,
                CommandPath);

        RuntimeHostCommandOperationResult result =
            await composition.CommandService.ExecuteAsync(
                target,
                expectedArgument);

        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            expectedReturnValue,
            result.ReturnValue);

        Assert.Equal(
            1,
            executeCallCount);

        Assert.Same(
            originalValue,
            runtimeProperty.CurrentValue);
    }

    private static IEndpointAttachmentCommandOperations
        CreateNativeOperations(
            RuntimeEndpoint runtimeEndpoint,
            object? expectedArgument,
            object? returnValue,
            Action recordExecution)
    {
        return new NativeEndpointAttachmentCommandOperations(
            runtimeEndpoint,
            TimeSpan.FromSeconds(
                1),
            (request, timeout, cancellationToken) =>
            {
                recordExecution();

                var commandRequest =
                    Assert.IsType<ExecuteCommandRequest>(
                        request);

                Assert.Equal(
                    InstrumentId,
                    commandRequest.InstrumentId);

                Assert.Equal(
                    CommandPath,
                    commandRequest.CommandPath);

                Assert.Equal(
                    expectedArgument,
                    commandRequest.Argument);

                return Task.FromResult<ProtocolMessage>(
                    new ExecuteCommandResponse(
                        commandRequest.CorrelationId,
                        ProtocolResult.Success,
                        returnValue));
            });
    }

    private static IEndpointAttachmentCommandOperations
        CreateCompactOperations(
            EndpointDescriptorDefinition descriptorDefinition,
            Action recordExecution)
    {
        var mapping =
            new CompactCommandMapping(
                compactCommandId: 0x01,
                InstrumentId,
                CommandPath);

        var commandMap =
            new CompactCommandMap(
                descriptorDefinition,
                [
                    mapping
                ]);

        return new CompactEndpointAttachmentCommandOperations(
            commandMap,
            (compactCommandId, cancellationToken) =>
            {
                recordExecution();

                Assert.Equal(
                    mapping.CompactCommandId,
                    compactCommandId);

                return Task.FromResult(
                    CompactCommandExecutionStatus.Success);
            });
    }

    private static EndpointDescriptorDefinition
        CreateDescriptorDefinition()
    {
        var propertyDescriptor =
            new PropertyDescriptor(
                PropertyId,
                new DescriptorPath(
                    "Controller",
                    "State"),
                "Controller State",
                new BooleanDataDescriptor())
            {
                AccessMode =
                    PropertyAccessMode.ReadWrite
            };

        var commandDescriptor =
            new CommandDescriptor(
                CommandPath,
                "Toggle LED");

        var instrumentDescriptor =
            new InstrumentDescriptor(
                InstrumentId,
                "Controller",
                new InstrumentKind(
                    "controller"))
            {
                Interface =
                    new InstrumentInterface(
                        properties:
                        [
                            propertyDescriptor
                        ],
                        commands:
                        [
                            commandDescriptor
                        ])
            };

        return new EndpointDescriptorDefinition(
            new EndpointMetadata(),
            [
                instrumentDescriptor
            ]);
    }

    private sealed class StubConnectionDefinition
        : IEndpointConnectionDefinition
    {
        public StubConnectionDefinition(
            EndpointId expectedEndpointId)
        {
            ExpectedEndpointId =
                expectedEndpointId;
        }

        public EndpointConnectionOrigin Origin =>
            EndpointConnectionOrigin.Configured;

        public EndpointId? ExpectedEndpointId
        {
            get;
        }
    }

    private sealed class TestAttachmentInventory
        : IRuntimeEndpointAttachmentInventory
    {
        private readonly IReadOnlyList<
            RuntimeEndpointAttachmentInventoryEntry>
            _entries;

        public TestAttachmentInventory(
            params RuntimeEndpointAttachmentInventoryEntry[] entries)
        {
            _entries =
                entries.ToArray();
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
                    entry.EndpointId
                    == endpointId);
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

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}