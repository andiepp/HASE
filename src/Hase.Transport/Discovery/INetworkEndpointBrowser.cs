using System.Threading;

namespace Hase.Transport.Discovery;

/// <summary>
/// Browses the local network for possible HASE TCP endpoints.
/// </summary>
/// <remarks>
/// Discovered candidates are not verified HASE endpoints.
/// Every candidate must be verified separately through the HASE
/// Protocol Version 1 discovery exchange.
/// </remarks>
public interface INetworkEndpointBrowser
{
    /// <summary>
    /// Browses asynchronously for network endpoint candidates.
    /// </summary>
    /// <param name="cancellationToken">
    /// Stops browsing and candidate delivery.
    /// </param>
    /// <returns>
    /// An asynchronous stream of discovered candidates.
    /// </returns>
    IAsyncEnumerable<NetworkEndpointCandidate> BrowseAsync(
        CancellationToken cancellationToken = default);
}