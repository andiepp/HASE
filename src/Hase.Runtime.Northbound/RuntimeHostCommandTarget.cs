using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;

namespace Hase.Runtime.Northbound;

/// <summary>
/// Identifies one Command within one published runtime endpoint attachment.
/// </summary>
public sealed record RuntimeHostCommandTarget
{
    /// <summary>
    /// Initializes a generation-scoped runtime-host Command target.
    /// </summary>
    public RuntimeHostCommandTarget(
        EndpointId endpointId,
        RuntimeEndpointAttachmentGeneration attachmentGeneration,
        InstrumentId instrumentId,
        DescriptorPath commandPath)
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

        CommandPath =
            commandPath
            ?? throw new ArgumentNullException(
                nameof(commandPath));
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
    /// Gets the logical Command path.
    /// </summary>
    public DescriptorPath CommandPath
    {
        get;
    }
}