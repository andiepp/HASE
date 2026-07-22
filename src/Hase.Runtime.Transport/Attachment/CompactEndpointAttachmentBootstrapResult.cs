using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Contains the authoritative identity, exact repository reference, resolved
/// descriptor definition, and materialized descriptor obtained while
/// bootstrapping a compact serial endpoint for explicit attachment.
/// </summary>
public sealed class CompactEndpointAttachmentBootstrapResult
{
    /// <summary>
    /// Initializes a compact endpoint attachment bootstrap result.
    /// </summary>
    public CompactEndpointAttachmentBootstrapResult(
        EndpointId endpointId,
        DescriptorReference descriptorReference,
        EndpointDescriptorDefinition descriptorDefinition,
        EndpointDescriptor descriptor)
    {
        EndpointId =
            endpointId
            ?? throw new ArgumentNullException(
                nameof(endpointId));

        DescriptorReference =
            descriptorReference
            ?? throw new ArgumentNullException(
                nameof(descriptorReference));

        DescriptorDefinition =
            descriptorDefinition
            ?? throw new ArgumentNullException(
                nameof(descriptorDefinition));

        Descriptor =
            descriptor
            ?? throw new ArgumentNullException(
                nameof(descriptor));

        if (Descriptor.Id != EndpointId)
        {
            throw new ArgumentException(
                "The descriptor endpoint identity must match the "
                + "authoritative compact bootstrap endpoint identity.",
                nameof(descriptor));
        }
    }

    /// <summary>
    /// Gets the authoritative endpoint identity returned by compact
    /// bootstrap.
    /// </summary>
    public EndpointId EndpointId
    {
        get;
    }

    /// <summary>
    /// Gets the exact versioned descriptor reference returned by compact
    /// bootstrap.
    /// </summary>
    public DescriptorReference DescriptorReference
    {
        get;
    }

    /// <summary>
    /// Gets the exact descriptor definition resolved from the runtime host's
    /// repository.
    /// </summary>
    public EndpointDescriptorDefinition DescriptorDefinition
    {
        get;
    }

    /// <summary>
    /// Gets the descriptor definition materialized with the authoritative
    /// endpoint identity.
    /// </summary>
    public EndpointDescriptor Descriptor
    {
        get;
    }
}