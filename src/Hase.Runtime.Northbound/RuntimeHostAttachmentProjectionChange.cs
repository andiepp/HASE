namespace Hase.Runtime.Northbound;

/// <summary>
/// Represents one immutable ordered change from the shared attachment
/// projection.
/// </summary>
internal sealed record RuntimeHostAttachmentProjectionChange
{
    public RuntimeHostAttachmentProjectionChange(
        long order,
        RuntimeHostAttachmentProjectionChangeKind kind,
        RuntimeHostPublishedAttachment attachment,
        DateTimeOffset? endedAtUtc = null)
    {
        if (order <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(order),
                order,
                "Projection change order must be greater than zero.");
        }

        if (!Enum.IsDefined(
                kind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "Projection change kind must be defined.");
        }

        Attachment =
            attachment
            ?? throw new ArgumentNullException(
                nameof(attachment));

        if (endedAtUtc.HasValue
            && endedAtUtc.Value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The attachment end time must be expressed in UTC.",
                nameof(endedAtUtc));
        }

        if (kind == RuntimeHostAttachmentProjectionChangeKind.Published
            && endedAtUtc.HasValue)
        {
            throw new ArgumentException(
                "A publication change must not contain an attachment end time.",
                nameof(endedAtUtc));
        }

        if (kind == RuntimeHostAttachmentProjectionChangeKind.Ended
            && !endedAtUtc.HasValue)
        {
            throw new ArgumentException(
                "An ending change must contain an attachment end time.",
                nameof(endedAtUtc));
        }

        Order =
            order;

        Kind =
            kind;

        EndedAtUtc =
            endedAtUtc;
    }

    public long Order
    {
        get;
    }

    public RuntimeHostAttachmentProjectionChangeKind Kind
    {
        get;
    }

    public RuntimeHostPublishedAttachment Attachment
    {
        get;
    }

    public DateTimeOffset? EndedAtUtc
    {
        get;
    }
}