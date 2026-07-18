using Hase.Runtime.Runtime;

namespace Hase.Runtime.Transport;

/// <summary>
/// Synchronizes a runtime endpoint through an established runtime protocol
/// connection.
/// </summary>
/// <remarks>
/// Implementations may perform protocol discovery, descriptor retrieval,
/// descriptor validation, and runtime-state synchronization.
///
/// The protocol connection must already be operational before this method is
/// invoked.
///
/// This contract is the protocol-level successor to the transport-based
/// <see cref="IRuntimeEndpointSynchronizer"/> contract. The transport-based
/// contract remains available while coordinator and test infrastructure are
/// migrated incrementally.
/// </remarks>
public interface IRuntimeProtocolEndpointSynchronizer
{
    /// <summary>
    /// Synchronizes the supplied runtime endpoint through the established
    /// runtime protocol connection.
    /// </summary>
    /// <param name="connection">
    /// Established protocol connection used for synchronization.
    /// </param>
    /// <param name="runtimeEndpoint">
    /// Existing runtime endpoint whose state is synchronized.
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel synchronization.
    /// </param>
    Task SynchronizeAsync(
        IRuntimeProtocolConnection connection,
        RuntimeEndpoint runtimeEndpoint,
        CancellationToken cancellationToken = default);
}