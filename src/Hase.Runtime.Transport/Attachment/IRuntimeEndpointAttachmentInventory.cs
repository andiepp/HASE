using Hase.Core.Domain.Identity;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Owns the runtime host's active endpoint attachment inventory.
/// </summary>
/// <remarks>
/// Inventory identity is the authoritative <see cref="EndpointId"/> exposed
/// by the attached runtime endpoint. Discovery and connection metadata do not
/// identify inventory entries. Attaching an endpoint whose authoritative
/// identity is already present must fail and must never replace the existing
/// attachment automatically.
/// </remarks>
public interface IRuntimeEndpointAttachmentInventory
    : IAsyncDisposable
{
    /// <summary>
    /// Explicitly attaches an endpoint and adds its established session to
    /// the inventory.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// An attachment with the same authoritative endpoint identity is already
    /// present. The existing attachment remains unchanged.
    /// </exception>
    Task<RuntimeEndpointAttachmentInventoryEntry> AttachAsync(
        EndpointAttachmentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an active attachment by authoritative endpoint identity.
    /// </summary>
    RuntimeEndpointAttachmentInventoryEntry? Find(
        EndpointId endpointId);

    /// <summary>
    /// Returns an immutable snapshot of the active attachments.
    /// </summary>
    IReadOnlyList<RuntimeEndpointAttachmentInventoryEntry> List();

    /// <summary>
    /// Removes and orderly shuts down the attachment with the supplied
    /// authoritative endpoint identity.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when an attachment was found and detached;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    Task<bool> DetachAsync(
        EndpointId endpointId,
        CancellationToken cancellationToken = default);
}