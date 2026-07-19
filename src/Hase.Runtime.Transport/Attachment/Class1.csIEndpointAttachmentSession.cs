using Hase.Runtime.Runtime;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Owns the complete active communication lifecycle for one attached
/// runtime endpoint.
/// </summary>
/// <remarks>
/// Implementations own all transport, protocol, synchronization,
/// recovery, health-probing, notification-routing, and diagnostic
/// resources created for the attachment.
/// </remarks>
public interface IEndpointAttachmentSession
    : IAsyncDisposable
{
    /// <summary>
    /// Gets the explicit request that created this attachment.
    /// </summary>
    EndpointAttachmentRequest Request
    {
        get;
    }

    /// <summary>
    /// Gets the attached and initially synchronized runtime endpoint.
    /// </summary>
    RuntimeEndpoint RuntimeEndpoint
    {
        get;
    }

    /// <summary>
    /// Performs an orderly shutdown of the attachment lifecycle.
    /// </summary>
    /// <remarks>
    /// Implementations must make repeated successful shutdown calls safe.
    /// Asynchronous disposal must also release the complete lifecycle.
    /// </remarks>
    Task ShutdownAsync(
        CancellationToken cancellationToken = default);
}