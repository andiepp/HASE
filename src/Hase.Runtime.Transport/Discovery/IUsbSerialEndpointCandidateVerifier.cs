using Hase.Transport.Discovery;
using Hase.Transport.Serial;

namespace Hase.Runtime.Transport.Discovery;

/// <summary>
/// Verifies whether a USB serial candidate is an authoritative,
/// compatible HASE compact endpoint.
/// </summary>
/// <remarks>
/// Candidate metadata is not authoritative endpoint identity.
/// Successful verification requires Compact Serial Protocol bootstrap,
/// exact descriptor resolution, and compatibility validation.
///
/// Verification never attaches or publishes a runtime endpoint.
/// </remarks>
public interface IUsbSerialEndpointCandidateVerifier
{
    /// <summary>
    /// Verifies one USB serial candidate.
    /// </summary>
    /// <param name="candidate">
    /// The candidate whose connection target is being verified.
    /// </param>
    /// <param name="transportOptions">
    /// The serial communication settings used for temporary verification.
    /// The configured port must identify the candidate's port.
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
    Task<UsbSerialEndpointVerificationResult> VerifyAsync(
        UsbSerialEndpointCandidate candidate,
        SerialTransportOptions transportOptions,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}