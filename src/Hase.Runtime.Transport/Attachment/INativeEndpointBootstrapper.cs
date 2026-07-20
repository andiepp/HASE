using Hase.Core.Domain.Identity;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Reads the authoritative identity and complete descriptor required
/// to construct a native HASE runtime endpoint.
/// </summary>
public interface INativeEndpointBootstrapper
{
    /// <summary>
    /// Bootstraps one native HASE endpoint through an established
    /// runtime protocol connection.
    /// </summary>
    /// <param name="connection">
    /// Established protocol connection used only for the bootstrap exchange.
    /// </param>
    /// <param name="expectedEndpointId">
    /// Expected authoritative endpoint identity, or
    /// <see langword="null"/> when no identity was selected or configured.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the bootstrap exchange.
    /// </param>
    Task<NativeEndpointBootstrapResult> BootstrapAsync(
        IRuntimeProtocolConnection connection,
        EndpointId? expectedEndpointId,
        CancellationToken cancellationToken = default);
}