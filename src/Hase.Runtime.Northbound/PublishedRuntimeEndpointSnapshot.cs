using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Runtime.Connections;

namespace Hase.Runtime.Northbound;

/// <summary>
/// Represents one immutable northbound snapshot of a published runtime
/// endpoint attachment.
/// </summary>
/// <remarks>
/// The snapshot exposes application-facing state only. It does not expose the
/// attachment session, mutable runtime endpoint, transport, protocol session,
/// connection coordinator, recovery supervisor, or notification router.
/// </remarks>
public sealed record PublishedRuntimeEndpointSnapshot
{
    /// <summary>
    /// Initializes a published runtime endpoint snapshot.
    /// </summary>
    public PublishedRuntimeEndpointSnapshot(
        RuntimeEndpointAttachmentGeneration generation,
        EndpointDescriptor descriptor,
        EndpointConnectionStatus connectionStatus)
    {
        Generation =
            generation
            ?? throw new ArgumentNullException(
                nameof(generation));

        Descriptor =
            descriptor
            ?? throw new ArgumentNullException(
                nameof(descriptor));

        ConnectionStatus =
            connectionStatus
            ?? throw new ArgumentNullException(
                nameof(connectionStatus));

        EndpointId =
            descriptor.Id;
    }

    /// <summary>
    /// Gets the authoritative endpoint identity derived from the endpoint
    /// descriptor.
    /// </summary>
    public EndpointId EndpointId
    {
        get;
    }

    /// <summary>
    /// Gets the opaque generation of the published attachment represented by
    /// this snapshot.
    /// </summary>
    public RuntimeEndpointAttachmentGeneration Generation
    {
        get;
    }

    /// <summary>
    /// Gets the immutable endpoint descriptor.
    /// </summary>
    public EndpointDescriptor Descriptor
    {
        get;
    }

    /// <summary>
    /// Gets the endpoint connection status captured by this snapshot.
    /// </summary>
    public EndpointConnectionStatus ConnectionStatus
    {
        get;
    }
}