using Hase.Protocol;
using Hase.Transport;

namespace Hase.Runtime.Transport;

/// <summary>
/// Performs one bounded protocol-level health probe against a native
/// endpoint and verifies that the responding endpoint retains its identity.
/// </summary>
internal sealed class NativeEndpointHealthProbe
{
    private static int _nextCorrelationId;

    private readonly RuntimeEndpointConnectionCoordinator _coordinator;
    private readonly NativeEndpointHealthProbeOptions _options;

    public NativeEndpointHealthProbe(
        RuntimeEndpointConnectionCoordinator coordinator,
        NativeEndpointHealthProbeOptions options)
    {
        _coordinator =
            coordinator
            ?? throw new ArgumentNullException(
                nameof(coordinator));

        _options =
            options
            ?? throw new ArgumentNullException(
                nameof(options));
    }

    public async Task ProbeAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var request =
            new DiscoverRequest(
                CreateCorrelationId());

        ProtocolMessage responseMessage =
            await _coordinator.ProbeAsync(
                request,
                _options.ProbeTimeout,
                cancellationToken);

        if (responseMessage
            is not DiscoverResponse response
            || response.EndpointId
                != _coordinator.RuntimeEndpoint.Descriptor.Id)
        {
            InvalidateCurrentTransport();

            throw new InvalidDataException(
                "The native endpoint health probe did not return the "
                + "expected authoritative endpoint identity.");
        }
    }

    private void InvalidateCurrentTransport()
    {
        ITransportConnection connection =
            _coordinator.ConnectionManager.CurrentConnection
            ?? throw new InvalidOperationException(
                "The native endpoint health probe does not have a "
                + "current transport connection to invalidate.");

        if (connection
            is not ITransportConnectionInvalidator invalidator)
        {
            throw new InvalidOperationException(
                "The native endpoint transport connection does not "
                + "support health-probe invalidation.");
        }

        invalidator.Invalidate();
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
