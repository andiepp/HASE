namespace Hase.Runtime.Northbound;

/// <summary>
/// Describes the ending of one published runtime endpoint attachment.
/// </summary>
public sealed record RuntimeHostAttachmentEndedObservationPayload
    : RuntimeHostObservationPayload
{
    /// <summary>
    /// Initializes an attachment-ended observation payload.
    /// </summary>
    public RuntimeHostAttachmentEndedObservationPayload(
        DateTimeOffset endedAtUtc)
    {
        if (endedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The attachment end time must be expressed in UTC.",
                nameof(endedAtUtc));
        }

        EndedAtUtc =
            endedAtUtc;
    }

    /// <inheritdoc />
    public override RuntimeHostObservationKind Kind =>
        RuntimeHostObservationKind.AttachmentEnded;

    /// <summary>
    /// Gets the host-observed UTC time at which the attachment ended.
    /// </summary>
    public DateTimeOffset EndedAtUtc
    {
        get;
    }
}