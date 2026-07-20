using Hase.Core.Domain.Identity;
using Hase.Protocol;
using Hase.Runtime.Runtime;
using Hase.Transport;

namespace Hase.Runtime.Transport.Attachment;

/// <summary>
/// Validates authoritative endpoint identity on the operational connection
/// before delegating descriptor and property synchronization.
/// </summary>
public sealed class IdentityValidatingRuntimeEndpointSynchronizer
    : IRuntimeEndpointSynchronizer,
      IRuntimeProtocolEndpointSynchronizer
{
    private static int _nextCorrelationId;

    private readonly IRuntimeEndpointSynchronizer
        _transportSynchronizer;

    private readonly IRuntimeProtocolEndpointSynchronizer
        _protocolSynchronizer;

    /// <summary>
    /// Initializes an identity-validating synchronizer decorator.
    /// </summary>
    public IdentityValidatingRuntimeEndpointSynchronizer(
        IRuntimeEndpointSynchronizer synchronizer)
    {
        _transportSynchronizer =
            synchronizer
            ?? throw new ArgumentNullException(
                nameof(synchronizer));

        _protocolSynchronizer =
            synchronizer
                as IRuntimeProtocolEndpointSynchronizer
            ?? throw new ArgumentException(
                "The decorated synchronizer must support runtime "
                + "protocol connections.",
                nameof(synchronizer));
    }

    /// <inheritdoc />
    public async Task SynchronizeAsync(
        ITransportConnection connection,
        RuntimeEndpoint runtimeEndpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            connection);

        ArgumentNullException.ThrowIfNull(
            runtimeEndpoint);

        cancellationToken.ThrowIfCancellationRequested();

        var protocolConnection =
            new LegacyRuntimeProtocolConnection(
                connection);

        await ValidateIdentityAsync(
            protocolConnection,
            runtimeEndpoint,
            cancellationToken);

        await _transportSynchronizer.SynchronizeAsync(
            connection,
            runtimeEndpoint,
            cancellationToken);
    }

    /// <inheritdoc />
    async Task IRuntimeProtocolEndpointSynchronizer.SynchronizeAsync(
        IRuntimeProtocolConnection connection,
        RuntimeEndpoint runtimeEndpoint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            connection);

        ArgumentNullException.ThrowIfNull(
            runtimeEndpoint);

        cancellationToken.ThrowIfCancellationRequested();

        await ValidateIdentityAsync(
            connection,
            runtimeEndpoint,
            cancellationToken);

        await _protocolSynchronizer.SynchronizeAsync(
            connection,
            runtimeEndpoint,
            cancellationToken);
    }

    private static async Task ValidateIdentityAsync(
        IRuntimeProtocolConnection connection,
        RuntimeEndpoint runtimeEndpoint,
        CancellationToken cancellationToken)
    {
        var request =
            new DiscoverRequest(
                CreateCorrelationId());

        ProtocolMessage responseMessage =
            await connection.SendAsync(
                request,
                cancellationToken);

        if (responseMessage
            is not DiscoverResponse response)
        {
            throw new InvalidDataException(
                "Operational endpoint identity validation expected a "
                + $"{nameof(DiscoverResponse)} but received "
                + $"'{responseMessage.GetType().Name}'.");
        }

        EndpointId authoritativeEndpointId =
            response.EndpointId
            ?? throw new InvalidDataException(
                "The operational discovery response did not contain "
                + "an authoritative endpoint identity.");

        EndpointId expectedEndpointId =
            runtimeEndpoint.Descriptor.Id;

        if (authoritativeEndpointId
            != expectedEndpointId)
        {
            throw new InvalidDataException(
                $"The operational endpoint identity "
                + $"'{authoritativeEndpointId.Value}' does not match "
                + $"the runtime endpoint identity "
                + $"'{expectedEndpointId.Value}'.");
        }
    }

    private static CorrelationId CreateCorrelationId()
    {
        uint value =
            unchecked(
                (uint)Interlocked.Increment(
                    ref _nextCorrelationId));

        if (value == CorrelationId.None.Value)
        {
            value =
                unchecked(
                    (uint)Interlocked.Increment(
                        ref _nextCorrelationId));
        }

        return new CorrelationId(
            value);
    }
}