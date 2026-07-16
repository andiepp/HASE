using Hase.Runtime.Runtime;
using Hase.Transport;

namespace Hase.Runtime.Transport;

/// <summary>
/// Synchronizes a runtime endpoint with a physical endpoint through an
/// established transport connection.
/// </summary>
/// <remarks>
/// Implementations may perform protocol discovery, descriptor retrieval,
/// descriptor validation, and runtime-state synchronization.
///
/// The transport connection must already be established before this
/// operation is invoked.
/// </remarks>
public interface IRuntimeEndpointSynchronizer
{
    /// <summary>
    /// Synchronizes the supplied runtime endpoint through the established
    /// transport connection.
    /// </summary>
    /// <param name="connection">
    /// Established transport connection used for synchronization.
    /// </param>
    /// <param name="runtimeEndpoint">
    /// Existing runtime endpoint whose state is synchronized.
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel synchronization.
    /// </param>
    Task SynchronizeAsync(
        ITransportConnection connection,
        RuntimeEndpoint runtimeEndpoint,
        CancellationToken cancellationToken = default);
}