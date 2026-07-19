namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Explicitly attaches endpoints to a HASE runtime host.
/// </summary>
public interface IEndpointAttachmentService
{
    /// <summary>
    /// Creates and starts one endpoint attachment lifecycle.
    /// </summary>
    /// <param name="request">
    /// The explicit connection and descriptor-source selection.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels attachment and requires cleanup of all resources created
    /// by the incomplete attempt.
    /// </param>
    /// <returns>
    /// An attachment session after identity verification, descriptor
    /// resolution, runtime construction, and initial synchronization
    /// have completed successfully.
    /// </returns>
    Task<IEndpointAttachmentSession> AttachAsync(
        EndpointAttachmentRequest request,
        CancellationToken cancellationToken = default);
}
