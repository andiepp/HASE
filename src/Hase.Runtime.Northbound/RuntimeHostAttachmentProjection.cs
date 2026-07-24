using Hase.Core.Domain.Identity;
using Hase.Runtime.Transport.Attachment;

namespace Hase.Runtime.Northbound;

/// <summary>
/// Maintains one shared projection of current attachment entries and their
/// authoritative northbound generations.
/// </summary>
internal sealed class RuntimeHostAttachmentProjection
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

    public RuntimeHostAttachmentProjection(
        IRuntimeEndpointAttachmentInventory attachmentInventory)
    {
        _attachmentInventory =
            attachmentInventory
            ?? throw new ArgumentNullException(
                nameof(attachmentInventory));
    }

    /// <summary>
    /// Lists current published attachments with stable per-entry generations.
    /// </summary>
    public IReadOnlyList<RuntimeHostPublishedAttachment> List()
    {
        lock (_syncRoot)
        {
            IReadOnlyList<RuntimeEndpointAttachmentInventoryEntry> entries =
                _attachmentInventory.List();

            RetireEndedEntries(
                entries);

            var attachments =
                new RuntimeHostPublishedAttachment[entries.Count];

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

                attachments[index] =
                    new RuntimeHostPublishedAttachment(
                        entry,
                        generation);
            }

            return attachments;
        }
    }

    /// <summary>
    /// Finds one current published attachment by authoritative endpoint
    /// identity.
    /// </summary>
    public RuntimeHostPublishedAttachment? Find(
        EndpointId endpointId)
    {
        ArgumentNullException.ThrowIfNull(
            endpointId);

        return List().FirstOrDefault(
            attachment =>
                attachment.Entry.EndpointId
                == endpointId);
    }

    private void RetireEndedEntries(
        IReadOnlyList<RuntimeEndpointAttachmentInventoryEntry> entries)
    {
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
    }
}
