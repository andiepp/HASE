namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Describes one attachment that has become part of the authoritative runtime
/// endpoint attachment inventory.
/// </summary>
public sealed record RuntimeEndpointAttachmentPublished
{
    /// <summary>
    /// Initializes an attachment-publication notification.
    /// </summary>
    public RuntimeEndpointAttachmentPublished(
        RuntimeEndpointAttachmentInventoryEntry entry)
    {
        Entry =
            entry
            ?? throw new ArgumentNullException(
                nameof(entry));
    }

    /// <summary>
    /// Gets the committed inventory entry.
    /// </summary>
    public RuntimeEndpointAttachmentInventoryEntry Entry
    {
        get;
    }
}