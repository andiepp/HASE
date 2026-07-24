namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Exposes committed changes from the authoritative runtime endpoint
/// attachment inventory to trusted runtime-host composition.
/// </summary>
/// <remarks>
/// This read-only observation surface is separate from attachment
/// administration so existing inventory consumers do not acquire observation
/// responsibilities.
/// </remarks>
public interface IRuntimeEndpointAttachmentInventoryObservationSource
{
    /// <summary>
    /// Registers an observer for later committed inventory changes.
    /// </summary>
    /// <returns>
    /// An idempotent registration whose disposal stops later callbacks to that
    /// observer.
    /// </returns>
    IDisposable Subscribe(
        IRuntimeEndpointAttachmentInventoryObserver observer);
}