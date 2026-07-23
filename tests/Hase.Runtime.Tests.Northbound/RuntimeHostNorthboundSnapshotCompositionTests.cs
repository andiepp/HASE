using Hase.Core.Domain.Identity;
using Hase.Runtime.Northbound;
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

        Assert.False(
            attachmentInventory.IsDisposed);
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

    private sealed class TestAttachmentInventory
        : IRuntimeEndpointAttachmentInventory
    {
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
            return null;
        }

        public IReadOnlyList<RuntimeEndpointAttachmentInventoryEntry> List()
        {
            return [];
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