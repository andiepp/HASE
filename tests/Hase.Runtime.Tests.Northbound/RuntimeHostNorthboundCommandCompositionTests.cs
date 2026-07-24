using Hase.Core.Domain.Identity;
using Hase.Runtime.Northbound;
using Hase.Runtime.Transport.Attachment;

namespace Hase.Runtime.Tests.Northbound;

public sealed class RuntimeHostNorthboundCommandCompositionTests
{
    [Fact]
    public async Task CreateFileBackedAsync_ExposesCommandService()
    {
        string directoryPath =
            Path.Combine(
                Path.GetTempPath(),
                $"hase-command-composition-{Guid.NewGuid():N}");

        try
        {
            var attachmentInventory =
                new TestAttachmentInventory();

            RuntimeHostNorthboundSnapshotComposition composition =
                await RuntimeHostNorthboundSnapshotComposition
                    .CreateFileBackedAsync(
                        attachmentInventory,
                        Path.Combine(
                            directoryPath,
                            "runtime-host-identity.json"),
                        new RuntimeHostId(
                            "runtime-host-command-composition"));

            Assert.IsType<RuntimeHostCommandService>(
                composition.CommandService);

            Assert.IsType<RuntimeHostPropertyService>(
                composition.PropertyService);

            Assert.False(
                attachmentInventory.IsDisposed);
        }
        finally
        {
            if (Directory.Exists(
                    directoryPath))
            {
                Directory.Delete(
                    directoryPath,
                    recursive: true);
            }
        }
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
            return Array.Empty<
                RuntimeEndpointAttachmentInventoryEntry>();
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
}