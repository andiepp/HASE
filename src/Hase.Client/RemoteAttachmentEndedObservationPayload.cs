namespace Hase.Client;

/// <summary>
/// Describes the ending of one published remote endpoint attachment.
/// </summary>
public sealed record RemoteAttachmentEndedObservationPayload
    : RemoteObservationPayload
{
    /// <summary>
    /// Initializes one attachment-ended observation payload.
    /// </summary>
    public RemoteAttachmentEndedObservationPayload(
        DateTimeOffset endedAtUtc)
    {
        if (endedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "The remote attachment end time must be expressed in UTC.",
                nameof(endedAtUtc));
        }

        EndedAtUtc =
            endedAtUtc;
    }

    /// <inheritdoc />
    public override RemoteObservationKind Kind =>
        RemoteObservationKind.AttachmentEnded;

    /// <summary>
    /// Gets the host-observed UTC time at which the attachment ended.
    /// </summary>
    public DateTimeOffset EndedAtUtc
    {
        get;
    }
}
