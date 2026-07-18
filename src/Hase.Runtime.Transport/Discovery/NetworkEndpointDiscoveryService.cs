using System.Runtime.CompilerServices;
using Hase.Core.Domain.Identity;
using Hase.Transport.Discovery;

namespace Hase.Runtime.Transport.Discovery;

/// <summary>
/// Combines network endpoint browsing with authoritative HASE
/// Protocol Version 1 candidate verification.
/// </summary>
public sealed class NetworkEndpointDiscoveryService
    : INetworkEndpointDiscoveryService
{
    private readonly INetworkEndpointBrowser _browser;

    private readonly INetworkEndpointCandidateVerifier
        _verifier;

    /// <summary>
    /// Initializes a network endpoint discovery service.
    /// </summary>
    public NetworkEndpointDiscoveryService(
        INetworkEndpointBrowser browser,
        INetworkEndpointCandidateVerifier verifier)
    {
        _browser =
            browser
            ?? throw new ArgumentNullException(
                nameof(browser));

        _verifier =
            verifier
            ?? throw new ArgumentNullException(
                nameof(verifier));
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<
        NetworkEndpointVerificationResult> DiscoverAsync(
            TimeSpan verificationTimeout,
            [EnumeratorCancellation]
            CancellationToken cancellationToken = default)
    {
        if (verificationTimeout
                != Timeout.InfiniteTimeSpan
            && verificationTimeout
                <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(verificationTimeout),
                verificationTimeout,
                "The candidate verification timeout must be positive "
                + "or Timeout.InfiniteTimeSpan.");
        }

        cancellationToken
            .ThrowIfCancellationRequested();

        var verifiedEndpointIds =
            new HashSet<EndpointId>();

        await foreach (
            NetworkEndpointCandidate candidate
            in _browser
                .BrowseAsync(
                    cancellationToken)
                .WithCancellation(
                    cancellationToken))
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            NetworkEndpointVerificationResult result =
                await _verifier.VerifyAsync(
                    candidate,
                    verificationTimeout,
                    cancellationToken);

            if (result
                is VerifiedNetworkEndpoint verifiedEndpoint)
            {
                if (!verifiedEndpointIds.Add(
                        verifiedEndpoint.EndpointId))
                {
                    continue;
                }
            }

            yield return result;
        }
    }
}