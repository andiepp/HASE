namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Observes committed publication and ending changes in the authoritative
/// runtime endpoint attachment inventory.
/// </summary>
/// <remarks>
/// Observation is read-only. An observer does not acquire attachment lifecycle
/// ownership and cannot attach, detach, replace, shut down, or dispose an
/// endpoint through this contract.
/// </remarks>
public interface IRuntimeEndpointAttachmentInventoryObserver
{
    /// <summary>
    /// Receives one committed attachment-publication notification.
    /// </summary>
    void OnAttachmentPublished(
        RuntimeEndpointAttachmentPublished publication);

    /// <summary>
    /// Receives one committed attachment-ending notification.
    /// </summary>
    void OnAttachmentEnded(
        RuntimeEndpointAttachmentEnded ending);
}