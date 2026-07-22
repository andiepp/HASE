using Hase.Transport.Discovery;
using Hase.Transport.Serial;

namespace Hase.Runtime.Transport.Discovery;

/// <summary>
/// Performs the temporary compact endpoint verification operation after
/// public verifier inputs have been validated.
/// </summary>
internal interface IUsbSerialEndpointVerificationOperation
{
    Task<UsbSerialEndpointVerificationResult> VerifyAsync(
        UsbSerialEndpointCandidate candidate,
        SerialTransportOptions transportOptions,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}