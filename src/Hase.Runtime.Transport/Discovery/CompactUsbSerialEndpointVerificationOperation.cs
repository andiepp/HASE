using Hase.CompactProtocol;
using Hase.Transport.Discovery;
using Hase.Transport.Serial;

namespace Hase.Runtime.Transport.Discovery;

/// <summary>
/// Performs one temporary successful-path compact endpoint verification.
/// </summary>
internal sealed class CompactUsbSerialEndpointVerificationOperation
    : IUsbSerialEndpointVerificationOperation
{
    private readonly ICompactEndpointConnectionFactory _connectionFactory;

    public CompactUsbSerialEndpointVerificationOperation(
        ICompactEndpointConnectionFactory connectionFactory)
    {
        _connectionFactory =
            connectionFactory
            ?? throw new ArgumentNullException(
                nameof(connectionFactory));
    }

    public async Task<UsbSerialEndpointVerificationResult> VerifyAsync(
        UsbSerialEndpointCandidate candidate,
        SerialTransportOptions transportOptions,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            candidate);

        ArgumentNullException.ThrowIfNull(
            transportOptions);

        cancellationToken
            .ThrowIfCancellationRequested();

        _ = timeout;

        await using CompactEndpointConnection connection =
            await _connectionFactory.ConnectAsync(
                transportOptions,
                expectedEndpointId: null,
                cancellationToken);

        CompactEndpointInitializationResult initializationResult =
            connection.InitializationResult
            ?? throw new InvalidOperationException(
                "The temporary compact endpoint connection did not preserve "
                + "its initialization result.");

        return new VerifiedUsbSerialEndpoint(
            candidate,
            initializationResult.EndpointId,
            initializationResult.DescriptorReference,
            initializationResult.DescriptorDefinition);
    }
}