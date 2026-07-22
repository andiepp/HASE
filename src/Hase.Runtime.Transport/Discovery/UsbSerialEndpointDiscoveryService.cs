using Hase.Transport.Discovery;
using Hase.Transport.Serial;

namespace Hase.Runtime.Transport.Discovery;

/// <summary>
/// Discovers USB serial compact endpoints through candidate enumeration,
/// optional metadata filtering, and authoritative compact verification.
/// </summary>
/// <remarks>
/// Discovery is sequential and never attaches, publishes, replaces, or
/// otherwise mutates runtime endpoints.
/// </remarks>
public sealed class UsbSerialEndpointDiscoveryService
{
    private readonly IUsbSerialEndpointCandidateSource _candidateSource;
    private readonly IUsbSerialEndpointCandidateVerifier _candidateVerifier;
    private readonly IUsbSerialEndpointCandidateFilter? _candidateFilter;

    public UsbSerialEndpointDiscoveryService(
        IUsbSerialEndpointCandidateSource candidateSource,
        IUsbSerialEndpointCandidateVerifier candidateVerifier,
        IUsbSerialEndpointCandidateFilter? candidateFilter = null)
    {
        _candidateSource =
            candidateSource
            ?? throw new ArgumentNullException(
                nameof(candidateSource));

        _candidateVerifier =
            candidateVerifier
            ?? throw new ArgumentNullException(
                nameof(candidateVerifier));

        _candidateFilter =
            candidateFilter;
    }

    /// <summary>
    /// Enumerates and verifies one ordered snapshot of eligible USB serial
    /// candidates.
    /// </summary>
    public async Task<UsbSerialEndpointDiscoveryResult> DiscoverAsync(
        UsbSerialEndpointDiscoveryOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            options);

        cancellationToken.ThrowIfCancellationRequested();

        var results =
            new List<UsbSerialEndpointVerificationResult>();

        var observedCandidates =
            new HashSet<UsbSerialEndpointCandidate>();

        await foreach (
            UsbSerialEndpointCandidate candidate
            in _candidateSource
                .EnumerateAsync(
                    cancellationToken)
                .WithCancellation(
                    cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!observedCandidates.Add(
                candidate))
            {
                continue;
            }

            if (_candidateFilter is not null
                && !_candidateFilter.IsMatch(
                    candidate))
            {
                continue;
            }

            SerialTransportOptions transportOptions =
                options.CreateTransportOptions(
                    candidate.PortName);

            UsbSerialEndpointVerificationResult result =
                await _candidateVerifier.VerifyAsync(
                    candidate,
                    transportOptions,
                    options.VerificationTimeout,
                    cancellationToken);

            results.Add(
                result);
        }

        return new UsbSerialEndpointDiscoveryResult(
            results);
    }
}