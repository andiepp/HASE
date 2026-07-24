using Hase.Core.Domain.Data;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Connections;
using Hase.Runtime.Northbound;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;

namespace Hase.Runtime.Tests.Northbound;

public sealed class RuntimeHostCachedPropertyResolverTests
{
    [Fact]
    public void Constructor_NullProjection_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RuntimeHostCachedPropertyResolver(
                null!));
    }

    [Fact]
    public void GetCached_NullTarget_Throws()
    {
        TestFixture fixture =
            CreateFixture();

        Assert.Throws<ArgumentNullException>(
            () => fixture.Resolver.GetCached(
                null!));
    }

    [Fact]
    public void GetCached_MissingAttachment_ReturnsNotCurrent()
    {
        TestFixture fixture =
            CreateFixture();

        fixture.Inventory.SetEntries();

        RuntimeHostCachedPropertyResult result =
            fixture.Resolver.GetCached(
                fixture.Target);

        Assert.Equal(
            RuntimeHostPropertyOperationStatus.AttachmentNotCurrent,
            result.Status);

        Assert.Null(
            result.Snapshot);
    }

    [Fact]
    public void GetCached_DifferentGeneration_ReturnsNotCurrent()
    {
        TestFixture fixture =
            CreateFixture();

        var target =
            new RuntimeHostPropertyTarget(
                fixture.Target.EndpointId,
                RuntimeEndpointAttachmentGeneration.CreateNew(),
                fixture.Target.InstrumentId,
                fixture.Target.PropertyId);

        RuntimeHostCachedPropertyResult result =
            fixture.Resolver.GetCached(
                target);

        Assert.Equal(
            RuntimeHostPropertyOperationStatus.AttachmentNotCurrent,
            result.Status);
    }

    [Fact]
    public void GetCached_MissingInstrument_ReturnsInstrumentNotFound()
    {
        TestFixture fixture =
            CreateFixture();

        var target =
            new RuntimeHostPropertyTarget(
                fixture.Target.EndpointId,
                fixture.Target.AttachmentGeneration,
                new InstrumentId(
                    "missing-instrument"),
                fixture.Target.PropertyId);

        RuntimeHostCachedPropertyResult result =
            fixture.Resolver.GetCached(
                target);

        Assert.Equal(
            RuntimeHostPropertyOperationStatus.InstrumentNotFound,
            result.Status);
    }

    [Fact]
    public void GetCached_MissingProperty_ReturnsPropertyNotFound()
    {
        TestFixture fixture =
            CreateFixture();

        var target =
            new RuntimeHostPropertyTarget(
                fixture.Target.EndpointId,
                fixture.Target.AttachmentGeneration,
                fixture.Target.InstrumentId,
                new PropertyId(
                    "missing-property"));

        RuntimeHostCachedPropertyResult result =
            fixture.Resolver.GetCached(
                target);

        Assert.Equal(
            RuntimeHostPropertyOperationStatus.PropertyNotFound,
            result.Status);
    }

    [Fact]
    public void GetCached_UnknownValue_ReturnsSuccessfulUnknownSnapshot()
    {
        TestFixture fixture =
            CreateFixture();

        RuntimeHostCachedPropertyResult result =
            fixture.Resolver.GetCached(
                fixture.Target);

        Assert.True(
            result.IsSuccess);

        Assert.NotNull(
            result.Snapshot);

        Assert.False(
            result.Snapshot.IsKnown);

        Assert.Null(
            result.Snapshot.CurrentValue);
    }

    [Fact]
    public void GetCached_KnownValueWhileFaulted_ReturnsCachedSnapshot()
    {
        TestFixture fixture =
            CreateFixture();

        RuntimeProperty runtimeProperty =
            fixture.Entry.RuntimeEndpoint
                .FindInstrument(
                    fixture.Target.InstrumentId)!
                .FindProperty(
                    fixture.Target.PropertyId)!;

        PropertyValue propertyValue =
            new(
                42,
                DateTimeOffset.UnixEpoch);

        runtimeProperty.UpdateValue(
            propertyValue);

        EndpointConnectionStatus connectionStatus =
            new(
                EndpointConnectionState.Faulted,
                DateTimeOffset.UnixEpoch,
                "Recovering.");

        fixture.Entry.RuntimeEndpoint.UpdateConnectionStatus(
            connectionStatus);

        RuntimeHostCachedPropertyResult result =
            fixture.Resolver.GetCached(
                fixture.Target);

        Assert.True(
            result.IsSuccess);

        Assert.NotNull(
            result.Snapshot);

        Assert.Same(
            propertyValue,
            result.Snapshot.CurrentValue);

        Assert.True(
            result.Snapshot.IsKnown);

        Assert.Same(
            connectionStatus,
            result.Snapshot.ConnectionStatus);
    }

    [Fact]
    public void GetCached_ReattachedIdentity_RejectsEarlierGeneration()
    {
        TestFixture fixture =
            CreateFixture();

        fixture.Inventory.SetEntries();

        Assert.Empty(
            fixture.Projection.List());

        RuntimeEndpointAttachmentInventoryEntry replacementEntry =
            CreateEntry();

        fixture.Inventory.SetEntries(
            replacementEntry);

        RuntimeHostCachedPropertyResult result =
            fixture.Resolver.GetCached(
                fixture.Target);

        Assert.Equal(
            RuntimeHostPropertyOperationStatus.AttachmentNotCurrent,
            result.Status);
    }

    private static TestFixture CreateFixture()
    {
        RuntimeEndpointAttachmentInventoryEntry entry =
            CreateEntry();

        var inventory =
            new TestAttachmentInventory(
                entry);

        var projection =
            new RuntimeHostAttachmentProjection(
                inventory);

        RuntimeHostPublishedAttachment attachment =
            Assert.Single(
                projection.List());

        var target =
            new RuntimeHostPropertyTarget(
                entry.EndpointId,
                attachment.Generation,
                new InstrumentId(
                    "instrument-one"),
                new PropertyId(
                    "property-one"));

        return new TestFixture(
            inventory,
            projection,
            entry,
            target,
            new RuntimeHostCachedPropertyResolver(
                projection));
    }

    private static RuntimeEndpointAttachmentInventoryEntry CreateEntry()
    {
        var propertyDescriptor =
            new PropertyDescriptor(
                new PropertyId(
                    "property-one"),
                new DescriptorPath(
                    "Instrument",
                    "Property"),
                "Property",
                new StringDataDescriptor());

        var instrumentDescriptor =
            new InstrumentDescriptor(
                new InstrumentId(
                    "instrument-one"),
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
                runtimeEndpoint));
    }

    private sealed record TestFixture(
        TestAttachmentInventory Inventory,
        RuntimeHostAttachmentProjection Projection,
        RuntimeEndpointAttachmentInventoryEntry Entry,
        RuntimeHostPropertyTarget Target,
        RuntimeHostCachedPropertyResolver Resolver);

    private sealed class TestAttachmentInventory
        : IRuntimeEndpointAttachmentInventory
    {
        private IReadOnlyList<RuntimeEndpointAttachmentInventoryEntry>
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

        public void SetEntries(
            params RuntimeEndpointAttachmentInventoryEntry[] entries)
        {
            _entries =
                entries.ToArray();
        }
    }

    private sealed class TestEndpointAttachmentSession
        : IEndpointAttachmentSession
    {
        public TestEndpointAttachmentSession(
            RuntimeEndpoint runtimeEndpoint)
        {
            RuntimeEndpoint =
                runtimeEndpoint;

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
}