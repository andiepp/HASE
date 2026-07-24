namespace Hase.Runtime.Northbound;

/// <summary>
/// Captures current shared attachment state and its internal ordering boundary.
/// </summary>
internal sealed record RuntimeHostAttachmentProjectionSnapshot
{
    public RuntimeHostAttachmentProjectionSnapshot(
        long changeOrder,
        IEnumerable<RuntimeHostPublishedAttachment> attachments)
    {
        if (changeOrder < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(changeOrder));
        }

        ArgumentNullException.ThrowIfNull(
            attachments);

        ChangeOrder =
            changeOrder;

        Attachments =
            Array.AsReadOnly(
                attachments.ToArray());
    }

    public long ChangeOrder
    {
        get;
    }

    public IReadOnlyList<RuntimeHostPublishedAttachment> Attachments
    {
        get;
    }
}