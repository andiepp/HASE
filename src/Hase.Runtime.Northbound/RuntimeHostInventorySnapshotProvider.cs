using Hase.Runtime.Transport.Attachment;

namespace Hase.Runtime.Northbound;

/// <summary>
/// Projects the authoritative runtime-host attachment inventory into
/// immutable northbound endpoint snapshots.
/// </summary>
/// <remarks>
/// One opaque generation is retained for the lifetime of each published
/// inventory-entry object. A later entry receives a new generation even when
/// it exposes the same authoritative endpoint identity.
/// </remarks>
public sealed class RuntimeHostInventorySnapshotProvider
    : IRuntimeHostInventorySnapshotProvider
{
    private readonly IRuntimeEndpointAttachmentInventory
        _attachmentInventory;

    private readonly Dictionary<
        RuntimeEndpointAttachmentInventoryEntry,
        RuntimeEndpointAttachmentGeneration>
        _generations =
            new(
                ReferenceEqualityComparer.Instance);

    private readonly object _syncRoot =
        new();

    /// <summary>
    /// Initializes a northbound inventory snapshot provider.
    /// </summary>
    public RuntimeHostInventorySnapshotProvider(
        IRuntimeEndpointAttachmentInventory attachmentInventory)
    {
        _attachmentInventory =
            attachmentInventory
            ?? throw new ArgumentNullException(
                nameof(attachmentInventory));
    }

    /// <inheritdoc />
    public IReadOnlyList<PublishedRuntimeEndpointSnapshot> List()
    {
        lock (_syncRoot)
        {
            IReadOnlyList<RuntimeEndpointAttachmentInventoryEntry> entries =
                _attachmentInventory.List();

            var currentEntries =
                new HashSet<RuntimeEndpointAttachmentInventoryEntry>(
                    entries,
                    ReferenceEqualityComparer.Instance);

            RuntimeEndpointAttachmentInventoryEntry[] endedEntries =
                _generations.Keys
                    .Where(
                        entry =>
                            !currentEntries.Contains(
                                entry))
                    .ToArray();

            foreach (
                RuntimeEndpointAttachmentInventoryEntry endedEntry
                in endedEntries)
            {
                _generations.Remove(
                    endedEntry);
            }

            var snapshots =
                new PublishedRuntimeEndpointSnapshot[entries.Count];

            for (int index = 0; index < entries.Count; index++)
            {
                RuntimeEndpointAttachmentInventoryEntry entry =
                    entries[index];

                if (!_generations.TryGetValue(
                        entry,
                        out var generation))
                {
                    generation =
                        RuntimeEndpointAttachmentGeneration.CreateNew();

                    _generations.Add(
                        entry,
                        generation);
                }

                snapshots[index] =
                    new PublishedRuntimeEndpointSnapshot(
                        generation,
                        entry.RuntimeEndpoint.Descriptor,
                        entry.RuntimeEndpoint.ConnectionStatus);
            }

            return snapshots;
        }
    }
}