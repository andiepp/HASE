using Hase.CompactProtocol;
using Hase.Runtime.Connections;

namespace Hase.Runtime.Transport;

/// <summary>
/// Performs one bounded compact endpoint health probe by refreshing all mapped
/// runtime properties through the active compact connection.
/// </summary>
internal sealed class CompactEndpointHealthProbe
{
    private readonly CompactRuntimeEndpointConnectionCoordinator _coordinator;
    private readonly CompactPropertyMap _propertyMap;
    private readonly CompactEndpointHealthProbeOptions _options;

    public CompactEndpointHealthProbe(
        CompactRuntimeEndpointConnectionCoordinator coordinator,
        CompactPropertyMap propertyMap,
        CompactEndpointHealthProbeOptions options)
    {
        _coordinator =
            coordinator
            ?? throw new ArgumentNullException(
                nameof(coordinator));

        _propertyMap =
            propertyMap
            ?? throw new ArgumentNullException(
                nameof(propertyMap));

        _options =
            options
            ?? throw new ArgumentNullException(
                nameof(options));
    }

    public async Task ProbeAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        CompactEndpointConnection connection =
            _coordinator.ActiveConnection
            ?? throw new InvalidOperationException(
                "A compact endpoint health probe requires an active "
                + "connection.");

        using var timeoutTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        timeoutTokenSource.CancelAfter(
            _options.ProbeTimeout);

        try
        {
            var synchronizer =
                new CompactRuntimePropertySynchronizer(
                    connection.Connection,
                    _propertyMap);

            IReadOnlyList<
                CompactRuntimePropertySynchronizationResult> results =
                await synchronizer.SynchronizeAsync(
                    _coordinator.RuntimeEndpoint,
                    timeoutTokenSource.Token);

            CompactRuntimePropertySynchronizationResult?
                unsuccessfulResult =
                    results.FirstOrDefault(
                        result =>
                            !result.CacheUpdated);

            if (unsuccessfulResult is not null)
            {
                throw new InvalidDataException(
                    $"Compact property identifier "
                    + $"0x{unsuccessfulResult.Mapping.CompactPropertyId:X2} "
                    + $"returned '{unsuccessfulResult.Status}' during health "
                    + "probing.");
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
            when (timeoutTokenSource.IsCancellationRequested)
        {
            connection.Connection.Invalidate();

            _coordinator.MarkFaulted(
                $"Compact endpoint health probe timed out after "
                + $"{_options.ProbeTimeout}.");

            throw new TimeoutException(
                $"Compact endpoint health probe timed out after "
                + $"{_options.ProbeTimeout}.",
                exception);
        }
        catch (Exception exception)
        {
            connection.Connection.Invalidate();

            _coordinator.MarkFaulted(
                $"Compact endpoint health probe failed: "
                + $"{exception.Message}");

            throw;
        }
    }
}
