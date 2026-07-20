using Hase.Core.Domain.Identity;
using Hase.Runtime.Runtime;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Represents one immutable runtime-host attachment inventory entry.
/// </summary>
public sealed class RuntimeEndpointAttachmentInventoryEntry
{
    /// <summary>
    /// Initializes an entry from an established attachment session.
    /// </summary>
    /// <remarks>
    /// The authoritative endpoint identity is obtained from the attached
    /// <see cref="RuntimeEndpoint"/> rather than from discovery or connection
    /// metadata.
    /// </remarks>
    public RuntimeEndpointAttachmentInventoryEntry(
        IEndpointAttachmentSession attachmentSession)
    {
        AttachmentSession =
            attachmentSession
            ?? throw new ArgumentNullException(
                nameof(attachmentSession));

        RuntimeEndpoint =
            attachmentSession.RuntimeEndpoint
            ?? throw new ArgumentException(
                "The attachment session must expose a runtime endpoint.",
                nameof(attachmentSession));

        EndpointId =
            RuntimeEndpoint.Descriptor.Id;
    }

    /// <summary>
    /// Gets the authoritative identity of the attached runtime endpoint.
    /// </summary>
    public EndpointId EndpointId
    {
        get;
    }

    /// <summary>
    /// Gets the attached runtime endpoint.
    /// </summary>
    public RuntimeEndpoint RuntimeEndpoint
    {
        get;
    }

    /// <summary>
    /// Gets the session that owns the endpoint communication lifecycle.
    /// </summary>
    public IEndpointAttachmentSession AttachmentSession
    {
        get;
    }
}