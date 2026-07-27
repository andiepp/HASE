using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;

namespace Hase.Client;

/// <summary>
/// Represents one immutable normalized snapshot of a published remote endpoint
/// attachment.
/// </summary>
public sealed record RemoteEndpointAttachmentSnapshot
{
    /// <summary>
    /// Initializes one published remote endpoint attachment snapshot.
    /// </summary>
    public RemoteEndpointAttachmentSnapshot(
        RemoteEndpointAttachmentGeneration generation,
        EndpointDescriptor descriptor,
        RemoteEndpointConnectionStatus connectionStatus)
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

        Key =
            new RemoteEndpointAttachmentKey(
                EndpointId,
                Generation);
    }

    /// <summary>
    /// Gets the authoritative endpoint identity derived from the descriptor.
    /// </summary>
    public EndpointId EndpointId
    {
        get;
    }

    /// <summary>
    /// Gets the opaque published attachment generation.
    /// </summary>
    public RemoteEndpointAttachmentGeneration Generation
    {
        get;
    }

    /// <summary>
    /// Gets the complete generation-scoped attachment key.
    /// </summary>
    public RemoteEndpointAttachmentKey Key
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
    /// Gets the captured physical endpoint connection status.
    /// </summary>
    public RemoteEndpointConnectionStatus ConnectionStatus
    {
        get;
    }
}
