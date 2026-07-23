using Hase.Core.Domain.Identity;

namespace Hase.Runtime.Northbound;

/// <summary>
/// Identifies one Property within one published runtime endpoint attachment.
/// </summary>
public sealed record RuntimeHostPropertyTarget
{
    /// <summary>
    /// Initializes a generation-scoped runtime-host Property target.
    /// </summary>
    public RuntimeHostPropertyTarget(
        EndpointId endpointId,
        RuntimeEndpointAttachmentGeneration attachmentGeneration,
        InstrumentId instrumentId,
        PropertyId propertyId)
    {
        EndpointId =
            endpointId
            ?? throw new ArgumentNullException(
                nameof(endpointId));

        AttachmentGeneration =
            attachmentGeneration
            ?? throw new ArgumentNullException(
                nameof(attachmentGeneration));

        InstrumentId =
            instrumentId
            ?? throw new ArgumentNullException(
                nameof(instrumentId));

        PropertyId =
            propertyId
            ?? throw new ArgumentNullException(
                nameof(propertyId));
    }

    /// <summary>
    /// Gets the authoritative endpoint identity.
    /// </summary>
    public EndpointId EndpointId
    {
        get;
    }

    /// <summary>
    /// Gets the expected published attachment generation.
    /// </summary>
    public RuntimeEndpointAttachmentGeneration AttachmentGeneration
    {
        get;
    }

    /// <summary>
    /// Gets the target instrument identity.
    /// </summary>
    public InstrumentId InstrumentId
    {
        get;
    }

    /// <summary>
    /// Gets the target Property identity.
    /// </summary>
    public PropertyId PropertyId
    {
        get;
    }
}