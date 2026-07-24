using Hase.Runtime.Transport.Attachment;

namespace Hase.Runtime.Northbound;

/// <summary>
/// Associates one currently published attachment entry with its authoritative
/// northbound attachment generation.
/// </summary>
internal sealed record RuntimeHostPublishedAttachment
{
    public RuntimeHostPublishedAttachment(
        RuntimeEndpointAttachmentInventoryEntry entry,
        RuntimeEndpointAttachmentGeneration generation)
    {
        Entry =
            entry
            ?? throw new ArgumentNullException(
                nameof(entry));

        Generation =
            generation
            ?? throw new ArgumentNullException(
                nameof(generation));
    }

    public RuntimeEndpointAttachmentInventoryEntry Entry
    {
        get;
    }

    public RuntimeEndpointAttachmentGeneration Generation
    {
        get;
    }
}