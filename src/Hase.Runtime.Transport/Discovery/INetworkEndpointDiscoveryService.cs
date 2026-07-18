namespace Hase.Runtime.Transport.Discovery;

/// <summary>
/// Browses for network endpoint candidates and verifies them as
/// authoritative HASE endpoints.
/// </summary>
public interface INetworkEndpointDiscoveryService
{
    /// <summary>
    /// Discovers and verifies HASE network endpoints.
    /// </summary>
    /// <param name="verificationTimeout">
    /// The maximum duration of each individual candidate verification.
    /// </param>
    /// <param name="cancellationToken">
    /// Stops browsing and active candidate verification.
    /// </param>
    /// <returns>
    /// A stream of unique verified endpoints and rejected candidates.
    /// </returns>
    IAsyncEnumerable<
        NetworkEndpointVerificationResult> DiscoverAsync(
            TimeSpan verificationTimeout,
            CancellationToken cancellationToken = default);
}