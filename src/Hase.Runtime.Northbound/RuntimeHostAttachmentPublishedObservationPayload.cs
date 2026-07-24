namespace Hase.Runtime.Northbound;

/// <summary>
/// Describes publication of one authoritative runtime endpoint attachment.
/// </summary>
public sealed record RuntimeHostAttachmentPublishedObservationPayload
    : RuntimeHostObservationPayload
{
    /// <summary>
    /// Initializes an attachment-published observation payload.
    /// </summary>
    public RuntimeHostAttachmentPublishedObservationPayload(
        PublishedRuntimeEndpointSnapshot endpoint)
    {
        Endpoint =
            endpoint
            ?? throw new ArgumentNullException(
                nameof(endpoint));
    }

    /// <inheritdoc />
    public override RuntimeHostObservationKind Kind =>
        RuntimeHostObservationKind.AttachmentPublished;

    /// <summary>
    /// Gets the complete immutable snapshot of the published attachment.
    /// </summary>
    public PublishedRuntimeEndpointSnapshot Endpoint
    {
        get;
    }
}