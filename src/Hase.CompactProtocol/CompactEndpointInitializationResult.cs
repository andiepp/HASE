using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;

namespace Hase.CompactProtocol;

/// <summary>
/// Preserves the authoritative compact bootstrap information, exact
/// descriptor definition, and materialized endpoint descriptor produced
/// during compact endpoint initialization.
/// </summary>
internal sealed class CompactEndpointInitializationResult
{
    public CompactEndpointInitializationResult(
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

        if (descriptor.Id != endpointId)
        {
            throw new ArgumentException(
                "The materialized descriptor identity must match the "
                + "authoritative compact endpoint identity.",
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
    /// Gets the exact descriptor reference returned by compact bootstrap.
    /// </summary>
    public DescriptorReference DescriptorReference
    {
        get;
    }

    /// <summary>
    /// Gets the exact descriptor definition resolved from the host
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