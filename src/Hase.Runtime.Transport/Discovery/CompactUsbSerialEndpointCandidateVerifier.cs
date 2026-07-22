using Hase.Transport.Discovery;
using Hase.Transport.Serial;

namespace Hase.Runtime.Transport.Discovery;

/// <summary>
/// Verifies USB serial candidates through temporary Compact Serial
/// Protocol bootstrap and exact descriptor resolution.
/// </summary>
public sealed class CompactUsbSerialEndpointCandidateVerifier
    : IUsbSerialEndpointCandidateVerifier
{
    private readonly IUsbSerialEndpointVerificationOperation _operation;

    internal CompactUsbSerialEndpointCandidateVerifier(
        IUsbSerialEndpointVerificationOperation operation)
    {
        ArgumentNullException.ThrowIfNull(
            operation);

        _operation =
            operation;
    }

    /// <inheritdoc />
    public async Task<UsbSerialEndpointVerificationResult> VerifyAsync(
        UsbSerialEndpointCandidate candidate,
        SerialTransportOptions transportOptions,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            candidate);

        ArgumentNullException.ThrowIfNull(
            transportOptions);

        if (!StringComparer.Ordinal.Equals(
                candidate.PortName,
                transportOptions.PortName))
        {
            throw new ArgumentException(
                "The serial transport port must match the candidate port.",
                nameof(transportOptions));
        }

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                timeout,
                "The verification timeout must be positive.");
        }

        cancellationToken
            .ThrowIfCancellationRequested();

        UsbSerialEndpointVerificationResult? result =
            await _operation.VerifyAsync(
                candidate,
                transportOptions,
                timeout,
                cancellationToken);

        return result
            ?? throw new InvalidOperationException(
                "The USB serial verification operation returned null.");
    }
}