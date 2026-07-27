using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Client;

/// <summary>
/// Identifies exactly one Command within one published remote endpoint
/// attachment.
/// </summary>
public sealed record RemoteCommandTarget
{
    /// <summary>
    /// Initializes one generation-scoped remote Command target.
    /// </summary>
    public RemoteCommandTarget(
        RemoteEndpointAttachmentKey attachment,
        InstrumentId instrumentId,
        DescriptorPath commandPath)
    {
        Attachment =
            attachment
            ?? throw new ArgumentNullException(
                nameof(attachment));

        InstrumentId =
            instrumentId
            ?? throw new ArgumentNullException(
                nameof(instrumentId));

        CommandPath =
            commandPath
            ?? throw new ArgumentNullException(
                nameof(commandPath));
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
    /// Gets the complete ordered logical Command path.
    /// </summary>
    public DescriptorPath CommandPath
    {
        get;
    }
}
