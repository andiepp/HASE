namespace Hase.Transport.Discovery;

/// <summary>
/// Determines whether a USB serial candidate is eligible for active
/// endpoint verification.
/// </summary>
/// <remarks>
/// A filter evaluates connection and USB metadata only.
/// A match does not verify HASE compatibility and does not establish
/// authoritative endpoint identity.
/// </remarks>
public interface IUsbSerialEndpointCandidateFilter
{
    /// <summary>
    /// Determines whether the candidate matches this filter.
    /// </summary>
    /// <param name="candidate">
    /// The USB serial candidate to evaluate.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the candidate is eligible for
    /// verification; otherwise, <see langword="false"/>.
    /// </returns>
    bool IsMatch(
        UsbSerialEndpointCandidate candidate);
}