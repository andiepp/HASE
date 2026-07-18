using Hase.Transport.Discovery;

namespace Hase.Runtime.Transport.Discovery;

/// <summary>
/// Verifies whether a discovered network candidate is an authoritative
/// HASE endpoint.
/// </summary>
public interface INetworkEndpointCandidateVerifier
{
    /// <summary>
    /// Verifies one discovered candidate.
    /// </summary>
    /// <param name="candidate">
    /// The candidate to verify.
    /// </param>
    /// <param name="timeout">
    /// The maximum duration of candidate verification.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels verification.
    /// </param>
    /// <returns>
    /// The successful or rejected verification result.
    /// </returns>
    Task<NetworkEndpointVerificationResult> VerifyAsync(
        NetworkEndpointCandidate candidate,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}