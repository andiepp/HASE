using Hase.Core.Domain.Identity;

namespace Hase.Runtime.Transport.Discovery;

/// <summary>
/// Represents one completed USB serial endpoint discovery operation.
/// </summary>
public sealed class UsbSerialEndpointDiscoveryResult
{
    internal UsbSerialEndpointDiscoveryResult(
        IEnumerable<UsbSerialEndpointVerificationResult> candidateResults)
    {
        ArgumentNullException.ThrowIfNull(
            candidateResults);

        UsbSerialEndpointVerificationResult[] retainedResults =
            candidateResults.ToArray();

        if (retainedResults.Any(
            result => result is null))
        {
            throw new ArgumentException(
                "Candidate discovery results must not contain null values.",
                nameof(candidateResults));
        }

        CandidateResults =
            retainedResults;

        var observedEndpointIds =
            new HashSet<EndpointId>();

        VerifiedEndpoints =
            retainedResults
                .OfType<VerifiedUsbSerialEndpoint>()
                .Where(
                    result => observedEndpointIds.Add(
                        result.EndpointId))
                .ToArray();
    }

    /// <summary>
    /// Gets every eligible distinct-port candidate outcome in source order.
    /// </summary>
    public IReadOnlyList<UsbSerialEndpointVerificationResult> CandidateResults
    {
        get;
    }

    /// <summary>
    /// Gets the unique authoritative endpoint inventory.
    /// </summary>
    /// <remarks>
    /// When multiple candidates report the same authoritative endpoint
    /// identity, the first verified result is retained in this inventory.
    /// Every original candidate outcome remains available through
    /// <see cref="CandidateResults"/>.
    /// </remarks>
    public IReadOnlyList<VerifiedUsbSerialEndpoint> VerifiedEndpoints
    {
        get;
    }
}
