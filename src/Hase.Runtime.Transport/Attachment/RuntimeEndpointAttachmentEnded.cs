namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Describes one attachment that has ended in the authoritative runtime
/// endpoint attachment inventory.
/// </summary>
public sealed record RuntimeEndpointAttachmentEnded
{
    /// <summary>
    /// Initializes an attachment-ending notification.
    /// </summary>
    public RuntimeEndpointAttachmentEnded(
        RuntimeEndpointAttachmentInventoryEntry entry,
        DateTimeOffset endedAtUtc)
    {
        Entry =
            entry
            ?? throw new ArgumentNullException(
                nameof(entry));

        if (endedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The attachment end time must be expressed in UTC.",
                nameof(endedAtUtc));
        }

        EndedAtUtc =
            endedAtUtc;
    }

    /// <summary>
    /// Gets the inventory entry whose publication ended.
    /// </summary>
    public RuntimeEndpointAttachmentInventoryEntry Entry
    {
        get;
    }

    /// <summary>
    /// Gets the host-observed UTC attachment end time.
    /// </summary>
    public DateTimeOffset EndedAtUtc
    {
        get;
    }
}