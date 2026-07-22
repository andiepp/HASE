using Hase.CompactProtocol;
using Hase.Core.Domain.Descriptors;
using Hase.Transport.Discovery;
using Hase.Transport.Serial;

namespace Hase.Runtime.Transport.Discovery;

/// <summary>
/// Composes production Windows USB serial endpoint discovery.
/// </summary>
/// <remarks>
/// Creating the discovery service does not enumerate devices, open serial
/// ports, exchange compact protocol messages, or attach runtime endpoints.
/// Discovery begins only when the returned service is invoked explicitly.
/// </remarks>
public static class WindowsUsbSerialEndpointDiscovery
{
    /// <summary>
    /// Creates a sequential Windows USB serial endpoint discovery service.
    /// </summary>
    /// <param name="descriptorRepository">
    /// The host repository used to resolve exact descriptor references
    /// reported by authoritative compact bootstrap.
    /// </param>
    /// <param name="candidateFilter">
    /// An optional metadata-only filter applied before active verification.
    /// A filter match does not assign endpoint identity or prove HASE
    /// compatibility.
    /// </param>
    public static UsbSerialEndpointDiscoveryService Create(
        IEndpointDescriptorRepository descriptorRepository,
        IUsbSerialEndpointCandidateFilter? candidateFilter = null)
    {
        ArgumentNullException.ThrowIfNull(
            descriptorRepository);

        var serialByteStreamFactory =
            new SystemIoPortsSerialByteStreamFactory();

        var connectionFactory =
            new CompactSerialEndpointConnector(
                serialByteStreamFactory,
                descriptorRepository);

        var verificationOperation =
            new CompactUsbSerialEndpointVerificationOperation(
                connectionFactory);

        var candidateVerifier =
            new CompactUsbSerialEndpointCandidateVerifier(
                verificationOperation);

        return new UsbSerialEndpointDiscoveryService(
            new WindowsUsbSerialEndpointCandidateSource(),
            candidateVerifier,
            candidateFilter);
    }
}