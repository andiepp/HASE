using Hase.CompactProtocol;
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

public sealed class RuntimeHostPropertyServiceEndpointFamilyIntegrationTests
{
    private static readonly InstrumentId InstrumentId =
        new(
            "controller-one");

    private static readonly PropertyId PropertyId =
        new(
            "controller.state");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PropertyService_NativeAndCompact_UseSameContract(
        bool useCompactEndpoint)
    {
        EndpointDescriptorDefinition descriptorDefinition =
            CreateDescriptorDefinition();

        var endpointId =
            new EndpointId(
                useCompactEndpoint
                    ? "compact-endpoint"
                    : "native-endpoint");

        RuntimeEndpoint runtimeEndpoint =
            new RuntimeContext()
                .AddEndpoint(
                    descriptorDefinition.Materialize(
                        endpointId));

        runtimeEndpoint.UpdateConnectionStatus(
            new EndpointConnectionStatus(
                EndpointConnectionState.Ready));

        RuntimeProperty runtimeProperty =
            GetRuntimeProperty(
                runtimeEndpoint);

        runtimeProperty.UpdateValue(
            CreatePropertyValue(
                true));

        IEndpointAttachmentPropertyOperations propertyOperations =
            useCompactEndpoint
                ? CreateCompactOperations(
                    descriptorDefinition,
                    runtimeEndpoint,
                    runtimeProperty)
                : CreateNativeOperations(
                    runtimeEndpoint);

        var session =
            new EndpointAttachmentSession(
                new EndpointAttachmentRequest(
                    new StubConnectionDefinition(
                        endpointId),
                    HostRepositoryDescriptorSource.Instance),
                runtimeEndpoint,
                propertyOperations,
                Array.Empty<IAsyncDisposable>());

        var inventory =
            new TestAttachmentInventory(
                new RuntimeEndpointAttachmentInventoryEntry(
                    session));

        RuntimeHostNorthboundSnapshotComposition composition =
            await RuntimeHostNorthboundSnapshotComposition
                .CreateFileBackedAsync(
                    inventory,
                    Path.Combine(
                        Path.GetTempPath(),
                        $"hase-property-integration-{Guid.NewGuid():N}",
                        "runtime-host-identity.json"),
                    new RuntimeHostId(
                        "runtime-host-property-integration"));

        PublishedRuntimeEndpointSnapshot endpointSnapshot =
            Assert.Single(
                composition.InventorySnapshotProvider.List());

        var target =
            new RuntimeHostPropertyTarget(
                endpointSnapshot.EndpointId,
                endpointSnapshot.Generation,
                InstrumentId,
                PropertyId);

        RuntimeHostCachedPropertyResult cachedResult =
            composition.PropertyService.GetCached(
                target);

        RuntimeHostPropertyOperationResult readResult =
            await composition.PropertyService.ReadAsync(
                target);

        RuntimeHostPropertyOperationResult writeResult =
            await composition.PropertyService.WriteAsync(
                target,
                true);

        Assert.True(
            cachedResult.IsSuccess);

        Assert.True(
            Assert.IsType<bool>(
                cachedResult.Snapshot?.CurrentValue?.Value));

        Assert.True(
            readResult.IsSuccess);

        Assert.False(
            Assert.IsType<bool>(
                readResult.ConfirmedValue?.Value));

        Assert.True(
            writeResult.IsSuccess);

        Assert.True(
            Assert.IsType<bool>(
                writeResult.ConfirmedValue?.Value));

        Assert.True(
            Assert.IsType<bool>(
                runtimeProperty.CurrentValue?.Value));
    }

    private static IEndpointAttachmentPropertyOperations
        CreateNativeOperations(
            RuntimeEndpoint runtimeEndpoint)
    {
        return new NativeEndpointAttachmentPropertyOperations(
            runtimeEndpoint,
            TimeSpan.FromSeconds(
                1),
            (
                request,
                timeout,
                cancellationToken) =>
            {
                ProtocolMessage response =
                    request switch
                    {
                        ReadPropertyRequest readRequest =>
                            new ReadPropertyResponse(
                                readRequest.CorrelationId,
                                ProtocolResult.Success,
                                CreatePropertyValue(
                                    false)),

                        WritePropertyRequest writeRequest =>
                            new WritePropertyResponse(
                                writeRequest.CorrelationId,
                                ProtocolResult.Success,
                                CreatePropertyValue(
                                    Assert.IsType<bool>(
                                        writeRequest.Value))),

                        _ =>
                            throw new InvalidDataException(
                                "Unexpected native Property request.")
                    };

                return Task.FromResult(
                    response);
            });
    }

    private static IEndpointAttachmentPropertyOperations
        CreateCompactOperations(
            EndpointDescriptorDefinition descriptorDefinition,
            RuntimeEndpoint runtimeEndpoint,
            RuntimeProperty runtimeProperty)
    {
        var mapping =
            new CompactPropertyMapping(
                compactPropertyId: 0x01,
                InstrumentId,
                PropertyId,
                CompactPropertyValueEncoding.Boolean);

        var propertyMap =
            new CompactPropertyMap(
                descriptorDefinition,
                [
                    mapping
                ]);

        return new CompactEndpointAttachmentPropertyOperations(
            propertyMap,
            (
                compactPropertyId,
                cancellationToken) =>
            {
                Assert.Equal(
                    mapping.CompactPropertyId,
                    compactPropertyId);

                runtimeProperty.UpdateValue(
                    CreatePropertyValue(
                        false));

                return Task.FromResult(
                    new CompactRuntimePropertySynchronizationResult(
                        mapping,
                        runtimeProperty,
                        CompactPropertyReadStatus.Success));
            },
            (
                compactPropertyId,
                requestedValue,
                cancellationToken) =>
            {
                Assert.Equal(
                    mapping.CompactPropertyId,
                    compactPropertyId);

                runtimeProperty.UpdateValue(
                    CreatePropertyValue(
                        Assert.IsType<bool>(
                            requestedValue)));

                return Task.FromResult(
                    new CompactRuntimePropertyWriteResult(
                        mapping,
                        runtimeProperty,
                        CompactPropertyWriteStatus.Success,
                        CompactPropertyReadStatus.Success));
            });
    }

    private static EndpointDescriptorDefinition
        CreateDescriptorDefinition()
    {
        var property =
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

        var instrument =
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
                            property
                        ])
            };

        return new EndpointDescriptorDefinition(
            instruments:
            [
                instrument
            ],
            metadata:
                new());
    }

    private static RuntimeProperty GetRuntimeProperty(
        RuntimeEndpoint runtimeEndpoint)
    {
        return runtimeEndpoint.FindInstrument(
                InstrumentId)
            ?.FindProperty(
                PropertyId)
            ?? throw new InvalidOperationException(
                "The integration runtime Property was not found.");
    }

    private static PropertyValue CreatePropertyValue(
        bool value)
    {
        return new PropertyValue(
            value,
            DateTimeOffset.UtcNow);
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

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
