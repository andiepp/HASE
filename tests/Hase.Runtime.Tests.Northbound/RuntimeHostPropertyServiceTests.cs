using Hase.Core.Domain.Data;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Northbound;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;

namespace Hase.Runtime.Tests.Northbound;

public sealed class RuntimeHostPropertyServiceTests
{
    private static readonly InstrumentId InstrumentId =
        new(
            "instrument-one");

    private static readonly PropertyId PropertyId =
        new(
            "property-one");

    [Fact]
    public void Contract_ExposesCachedReadAndWriteOperations()
    {
        Type serviceType =
            typeof(IRuntimeHostPropertyService);

        Assert.Equal(
            typeof(RuntimeHostCachedPropertyResult),
            serviceType
                .GetMethod(
                    nameof(IRuntimeHostPropertyService.GetCached))!
                .ReturnType);

        Assert.Equal(
            typeof(Task<RuntimeHostPropertyOperationResult>),
            serviceType
                .GetMethod(
                    nameof(IRuntimeHostPropertyService.ReadAsync))!
                .ReturnType);

        Assert.Equal(
            typeof(Task<RuntimeHostPropertyOperationResult>),
            serviceType
                .GetMethod(
                    nameof(IRuntimeHostPropertyService.WriteAsync))!
                .ReturnType);
    }

    [Fact]
    public void Constructor_NullProjection_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostPropertyService(
                null!));
    }

    [Fact]
    public async Task Service_UsesOneProjectionForCachedReadAndWrite()
    {
        var propertyOperations =
            new TestPropertyOperations();

        RuntimeEndpointAttachmentInventoryEntry entry =
            CreateEntry(
                propertyOperations);

        var projection =
            new RuntimeHostAttachmentProjection(
                new TestAttachmentInventory(
                    entry));

        RuntimeHostPublishedAttachment attachment =
            Assert.Single(
                projection.List());

        var target =
            new RuntimeHostPropertyTarget(
                entry.EndpointId,
                attachment.Generation,
                InstrumentId,
                PropertyId);

        IRuntimeHostPropertyService service =
            new RuntimeHostPropertyService(
                projection);

        RuntimeHostCachedPropertyResult cachedResult =
            service.GetCached(
                target);

        RuntimeHostPropertyOperationResult readResult =
            await service.ReadAsync(
                target);

        RuntimeHostPropertyOperationResult writeResult =
            await service.WriteAsync(
                target,
                true);

        Assert.True(
            cachedResult.IsSuccess);

        Assert.True(
            readResult.IsSuccess);

        Assert.True(
            writeResult.IsSuccess);

        Assert.Equal(
            1,
            propertyOperations.ReadCallCount);

        Assert.Equal(
            1,
            propertyOperations.WriteCallCount);
    }

    [Fact]
    public async Task Service_PreCancelledOperations_DoNotReachAttachment()
    {
        var propertyOperations =
            new TestPropertyOperations();

        RuntimeEndpointAttachmentInventoryEntry entry =
            CreateEntry(
                propertyOperations);

        var projection =
            new RuntimeHostAttachmentProjection(
                new TestAttachmentInventory(
                    entry));

        RuntimeHostPublishedAttachment attachment =
            Assert.Single(
                projection.List());

        var target =
            new RuntimeHostPropertyTarget(
                entry.EndpointId,
                attachment.Generation,
                InstrumentId,
                PropertyId);

        IRuntimeHostPropertyService service =
            new RuntimeHostPropertyService(
                projection);

        using var cancellationSource =
            new CancellationTokenSource();

        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ReadAsync(
                target,
                cancellationSource.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.WriteAsync(
                target,
                true,
                cancellationSource.Token));

        Assert.Equal(
            0,
            propertyOperations.ReadCallCount);

        Assert.Equal(
            0,
            propertyOperations.WriteCallCount);
    }

    private static RuntimeEndpointAttachmentInventoryEntry CreateEntry(
        TestPropertyOperations propertyOperations)
    {
        var propertyDescriptor =
            new PropertyDescriptor(
                PropertyId,
                new DescriptorPath(
                    "Instrument",
                    "Property"),
                "Property",
                new BooleanDataDescriptor())
            {
                AccessMode =
                    PropertyAccessMode.ReadWrite
            };

        var instrumentDescriptor =
            new InstrumentDescriptor(
                InstrumentId,
                "Instrument",
                new InstrumentKind(
                    "test"))
            {
                Interface =
                    new InstrumentInterface(
                        properties:
                        [
                            propertyDescriptor
                        ])
            };

        var runtimeEndpoint =
            new RuntimeEndpoint(
                new RuntimeContext(),
                new EndpointDescriptor(
                    new EndpointId(
                        "endpoint-one"),
                    [
                        instrumentDescriptor
                    ]));

        return new RuntimeEndpointAttachmentInventoryEntry(
            new TestEndpointAttachmentSession(
                runtimeEndpoint,
                propertyOperations));
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

    private sealed class TestEndpointAttachmentSession
        : IEndpointAttachmentSession
    {
        public TestEndpointAttachmentSession(
            RuntimeEndpoint runtimeEndpoint,
            IEndpointAttachmentPropertyOperations propertyOperations)
        {
            RuntimeEndpoint =
                runtimeEndpoint;

            PropertyOperations =
                propertyOperations;

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

        public IEndpointAttachmentPropertyOperations PropertyOperations
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

    private sealed class TestPropertyOperations
        : IEndpointAttachmentPropertyOperations
    {
        private readonly PropertyValue _confirmedValue =
            new(
                true,
                DateTimeOffset.UnixEpoch);

        public int ReadCallCount
        {
            get;
            private set;
        }

        public int WriteCallCount
        {
            get;
            private set;
        }

        public Task<EndpointAttachmentPropertyOperationResult> ReadAsync(
            InstrumentId instrumentId,
            PropertyId propertyId,
            CancellationToken cancellationToken = default)
        {
            ReadCallCount++;

            return Task.FromResult(
                EndpointAttachmentPropertyOperationResult.Successful(
                    _confirmedValue));
        }

        public Task<EndpointAttachmentPropertyOperationResult> WriteAsync(
            InstrumentId instrumentId,
            PropertyId propertyId,
            object? requestedValue,
            CancellationToken cancellationToken = default)
        {
            WriteCallCount++;

            return Task.FromResult(
                EndpointAttachmentPropertyOperationResult.Successful(
                    _confirmedValue));
        }
    }
}