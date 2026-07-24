using Hase.Core.Domain.Data;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Northbound;
using Hase.Runtime.Runtime;
using Hase.Runtime.Transport.Attachment;

namespace Hase.Runtime.Tests.Northbound;

public sealed class RuntimeHostNorthboundSnapshotCompositionTests
{
    [Fact]
    public async Task CreateFileBackedAsync_NullInventory_Throws()
    {
        using var directory =
            new TemporaryDirectory();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => RuntimeHostNorthboundSnapshotComposition
                .CreateFileBackedAsync(
                    null!,
                    Path.Combine(
                        directory.Path,
                        "runtime-host-identity.json")));
    }

    [Fact]
    public async Task CreateFileBackedAsync_ExplicitIdentity_SkipsStorage()
    {
        using var directory =
            new TemporaryDirectory();

        string identityDirectoryPath =
            Path.Combine(
                directory.Path,
                "identity");

        string identityFilePath =
            Path.Combine(
                identityDirectoryPath,
                "runtime-host-identity.json");

        var attachmentInventory =
            new TestAttachmentInventory();

        var configuredRuntimeHostId =
            new RuntimeHostId(
                "runtime-host-configured");

        RuntimeHostNorthboundSnapshotComposition composition =
            await RuntimeHostNorthboundSnapshotComposition
                .CreateFileBackedAsync(
                    attachmentInventory,
                    identityFilePath,
                    configuredRuntimeHostId);

        Assert.Same(
            configuredRuntimeHostId,
            composition.IdentityResolution.RuntimeHostId);

        Assert.Equal(
            RuntimeHostIdentityOrigin.ExplicitConfiguration,
            composition.IdentityResolution.Origin);

        Assert.Same(
            configuredRuntimeHostId,
            composition.SnapshotProvider
                .Capture()
                .RuntimeHostId);

        Assert.False(
            Directory.Exists(
                identityDirectoryPath));

        Assert.False(
            attachmentInventory.IsDisposed);
    }

    [Fact]
    public async Task CreateFileBackedAsync_FirstStartup_PersistsSnapshotIdentity()
    {
        using var directory =
            new TemporaryDirectory();

        string identityFilePath =
            Path.Combine(
                directory.Path,
                "runtime-host-identity.json");

        var attachmentInventory =
            new TestAttachmentInventory();

        RuntimeHostNorthboundSnapshotComposition composition =
            await RuntimeHostNorthboundSnapshotComposition
                .CreateFileBackedAsync(
                    attachmentInventory,
                    identityFilePath);

        Assert.Equal(
            RuntimeHostIdentityOrigin.GeneratedAndPersisted,
            composition.IdentityResolution.Origin);

        Assert.Equal(
            composition.IdentityResolution.RuntimeHostId,
            composition.SnapshotProvider
                .Capture()
                .RuntimeHostId);

        Assert.True(
            File.Exists(
                identityFilePath));

        Assert.IsType<RuntimeHostInventorySnapshotProvider>(
            composition.InventorySnapshotProvider);

        Assert.IsType<RuntimeHostSnapshotProvider>(
            composition.SnapshotProvider);

        Assert.IsType<RuntimeHostPropertyService>(
            composition.PropertyService);

        Assert.False(
            attachmentInventory.IsDisposed);
    }

    [Fact]
    public async Task CreateFileBackedAsync_SnapshotsAndPropertiesShareGeneration()
    {
        using var directory =
            new TemporaryDirectory();

        RuntimeEndpointAttachmentInventoryEntry entry =
            CreateEntry();

        RuntimeHostNorthboundSnapshotComposition composition =
            await RuntimeHostNorthboundSnapshotComposition
                .CreateFileBackedAsync(
                    new TestAttachmentInventory(
                        entry),
                    Path.Combine(
                        directory.Path,
                        "runtime-host-identity.json"),
                    new RuntimeHostId(
                        "runtime-host-shared-generation"));

        PublishedRuntimeEndpointSnapshot endpointSnapshot =
            Assert.Single(
                composition.InventorySnapshotProvider.List());

        var target =
            new RuntimeHostPropertyTarget(
                endpointSnapshot.EndpointId,
                endpointSnapshot.Generation,
                new InstrumentId(
                    "instrument-one"),
                new PropertyId(
                    "property-one"));

        RuntimeHostCachedPropertyResult result =
            composition.PropertyService.GetCached(
                target);

        Assert.True(
            result.IsSuccess);

        Assert.Equal(
            endpointSnapshot.Generation,
            result.Snapshot?.Target.AttachmentGeneration);
    }

    [Fact]
    public async Task CreateFileBackedAsync_Restart_ReusesPersistedIdentity()
    {
        using var directory =
            new TemporaryDirectory();

        string identityFilePath =
            Path.Combine(
                directory.Path,
                "runtime-host-identity.json");

        RuntimeHostNorthboundSnapshotComposition firstComposition =
            await RuntimeHostNorthboundSnapshotComposition
                .CreateFileBackedAsync(
                    new TestAttachmentInventory(),
                    identityFilePath);

        RuntimeHostNorthboundSnapshotComposition secondComposition =
            await RuntimeHostNorthboundSnapshotComposition
                .CreateFileBackedAsync(
                    new TestAttachmentInventory(),
                    identityFilePath);

        Assert.Equal(
            RuntimeHostIdentityOrigin.GeneratedAndPersisted,
            firstComposition.IdentityResolution.Origin);

        Assert.Equal(
            RuntimeHostIdentityOrigin.Persisted,
            secondComposition.IdentityResolution.Origin);

        Assert.Equal(
            firstComposition.IdentityResolution.RuntimeHostId,
            secondComposition.IdentityResolution.RuntimeHostId);
    }

    [Fact]
    public async Task CreateFileBackedAsync_PreCancelled_DoesNotCreateStorage()
    {
        using var directory =
            new TemporaryDirectory();

        string identityDirectoryPath =
            Path.Combine(
                directory.Path,
                "identity");

        using var cancellationSource =
            new CancellationTokenSource();

        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => RuntimeHostNorthboundSnapshotComposition
                .CreateFileBackedAsync(
                    new TestAttachmentInventory(),
                    Path.Combine(
                        identityDirectoryPath,
                        "runtime-host-identity.json"),
                    cancellationToken:
                        cancellationSource.Token));

        Assert.False(
            Directory.Exists(
                identityDirectoryPath));
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
                new BooleanDataDescriptor());

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

        public bool IsDisposed
        {
            get;
            private set;
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
            IsDisposed =
                true;

            return ValueTask.CompletedTask;
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

    private sealed class TemporaryDirectory
        : IDisposable
    {
        public TemporaryDirectory()
        {
            Path =
                System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    $"hase-runtime-host-composition-{Guid.NewGuid():N}");

            Directory.CreateDirectory(
                Path);
        }

        public string Path
        {
            get;
        }

        public void Dispose()
        {
            if (Directory.Exists(
                    Path))
            {
                Directory.Delete(
                    Path,
                    recursive: true);
            }
        }
    }
}