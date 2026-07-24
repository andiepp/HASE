using Hase.Core.Domain.Identity;

namespace Hase.Runtime.Northbound;

/// <summary>
/// Represents one immutable normalized observation within one runtime-host
/// observation subscription.
/// </summary>
public sealed record RuntimeHostObservation
{
    /// <summary>
    /// Initializes a runtime-host observation.
    /// </summary>
    public RuntimeHostObservation(
        RuntimeHostObservationSequence sequence,
        EndpointId endpointId,
        RuntimeEndpointAttachmentGeneration attachmentGeneration,
        RuntimeHostObservationPayload payload)
    {
        Sequence =
            sequence
            ?? throw new ArgumentNullException(
                nameof(sequence));

        EndpointId =
            endpointId
            ?? throw new ArgumentNullException(
                nameof(endpointId));

        AttachmentGeneration =
            attachmentGeneration
            ?? throw new ArgumentNullException(
                nameof(attachmentGeneration));

        Payload =
            payload
            ?? throw new ArgumentNullException(
                nameof(payload));

        if (payload
                is RuntimeHostAttachmentPublishedObservationPayload
                    attachmentPublished)
        {
            if (attachmentPublished.Endpoint.EndpointId
                != endpointId)
            {
                throw new ArgumentException(
                    "The published endpoint identity must match the observation.",
                    nameof(payload));
            }

            if (attachmentPublished.Endpoint.Generation
                != attachmentGeneration)
            {
                throw new ArgumentException(
                    "The published attachment generation must match the observation.",
                    nameof(payload));
            }
        }
    }

    /// <summary>
    /// Gets the subscription-local observation sequence.
    /// </summary>
    public RuntimeHostObservationSequence Sequence
    {
        get;
    }

    /// <summary>
    /// Gets the authoritative endpoint identity.
    /// </summary>
    public EndpointId EndpointId
    {
        get;
    }

    /// <summary>
    /// Gets the attachment generation from which the observation originated.
    /// </summary>
    public RuntimeEndpointAttachmentGeneration AttachmentGeneration
    {
        get;
    }

    /// <summary>
    /// Gets the normalized observation kind.
    /// </summary>
    public RuntimeHostObservationKind Kind =>
        Payload.Kind;

    /// <summary>
    /// Gets the immutable normalized observation payload.
    /// </summary>
    public RuntimeHostObservationPayload Payload
    {
        get;
    }
}