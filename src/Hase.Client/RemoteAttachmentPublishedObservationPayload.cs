namespace Hase.Client;

/// <summary>
/// Describes publication of one authoritative remote endpoint attachment.
/// </summary>
public sealed record RemoteAttachmentPublishedObservationPayload
    : RemoteObservationPayload
{
    /// <summary>
    /// Initializes one attachment-published observation payload.
    /// </summary>
    public RemoteAttachmentPublishedObservationPayload(
        RemoteEndpointAttachmentSnapshot endpoint)
    {
        Endpoint =
            endpoint
            ?? throw new ArgumentNullException(
                nameof(endpoint));
    }

    /// <inheritdoc />
    public override RemoteObservationKind Kind =>
        RemoteObservationKind.AttachmentPublished;

    /// <summary>
    /// Gets the complete immutable published attachment snapshot.
    /// </summary>
    public RemoteEndpointAttachmentSnapshot Endpoint
    {
        get;
    }
}
