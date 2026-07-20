using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Contains the authoritative identity and complete descriptor obtained
/// while bootstrapping a native HASE endpoint.
/// </summary>
public sealed class NativeEndpointBootstrapResult
{
    /// <summary>
    /// Initializes a native endpoint bootstrap result.
    /// </summary>
    public NativeEndpointBootstrapResult(
        EndpointId endpointId,
        EndpointDescriptor descriptor)
    {
        EndpointId =
            endpointId
            ?? throw new ArgumentNullException(
                nameof(endpointId));

        Descriptor =
            descriptor
            ?? throw new ArgumentNullException(
                nameof(descriptor));

        if (Descriptor.Id != EndpointId)
        {
            throw new ArgumentException(
                "The descriptor endpoint identity must match the "
                + "authoritative bootstrap endpoint identity.",
                nameof(descriptor));
        }
    }

    /// <summary>
    /// Gets the authoritative endpoint identity returned by
    /// Protocol Version 1 discovery.
    /// </summary>
    public EndpointId EndpointId
    {
        get;
    }

    /// <summary>
    /// Gets the complete descriptor returned by the endpoint.
    /// </summary>
    public EndpointDescriptor Descriptor
    {
        get;
    }
}