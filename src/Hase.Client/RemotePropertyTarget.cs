using Hase.Core.Domain.Identity;

namespace Hase.Client;

/// <summary>
/// Identifies exactly one Property within one published remote endpoint
/// attachment.
/// </summary>
public sealed record RemotePropertyTarget
{
    /// <summary>
    /// Initializes one generation-scoped remote Property target.
    /// </summary>
    public RemotePropertyTarget(
        RemoteEndpointAttachmentKey attachment,
        InstrumentId instrumentId,
        PropertyId propertyId)
    {
        Attachment =
            attachment
            ?? throw new ArgumentNullException(
                nameof(attachment));

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
    /// Gets the exact published endpoint attachment.
    /// </summary>
    public RemoteEndpointAttachmentKey Attachment
    {
        get;
    }

    /// <summary>
    /// Gets the authoritative endpoint identity.
    /// </summary>
    public EndpointId EndpointId =>
        Attachment.EndpointId;

    /// <summary>
    /// Gets the expected attachment generation.
    /// </summary>
    public RemoteEndpointAttachmentGeneration AttachmentGeneration =>
        Attachment.Generation;

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
