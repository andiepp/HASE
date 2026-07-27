namespace Hase.Client;

/// <summary>
/// Represents one immutable normalized observation within one remote
/// runtime-host observation subscription.
/// </summary>
public sealed record RemoteRuntimeHostObservation
{
    /// <summary>
    /// Initializes one remote runtime-host observation.
    /// </summary>
    public RemoteRuntimeHostObservation(
        RemoteObservationSequence sequence,
        RemoteEndpointAttachmentKey attachment,
        RemoteObservationPayload payload)
    {
        Sequence =
            sequence
            ?? throw new ArgumentNullException(
                nameof(sequence));

        Attachment =
            attachment
            ?? throw new ArgumentNullException(
                nameof(attachment));

        Payload =
            payload
            ?? throw new ArgumentNullException(
                nameof(payload));

        if (payload
                is RemoteAttachmentPublishedObservationPayload
                    attachmentPublished
            && attachmentPublished.Endpoint.Key
                != attachment)
        {
            throw new ArgumentException(
                "The published endpoint attachment key must match the "
                + "observation envelope.",
                nameof(payload));
        }
    }

    /// <summary>
    /// Gets the subscription-local observation sequence.
    /// </summary>
    public RemoteObservationSequence Sequence
    {
        get;
    }

    /// <summary>
    /// Gets the exact endpoint attachment from which the observation
    /// originated.
    /// </summary>
    public RemoteEndpointAttachmentKey Attachment
    {
        get;
    }

    /// <summary>
    /// Gets the normalized observation kind.
    /// </summary>
    public RemoteObservationKind Kind =>
        Payload.Kind;

    /// <summary>
    /// Gets the immutable normalized observation payload.
    /// </summary>
    public RemoteObservationPayload Payload
    {
        get;
    }
}
