using Hase.CompactProtocol;

namespace Hase.Runtime.Transport;

/// <summary>
/// Supervises one compact runtime endpoint through initial connection,
/// periodic health probing, fault recovery, reconnection, resynchronization,
/// and cancellation-aware shutdown.
/// </summary>
internal sealed class CompactRuntimeEndpointConnectionSupervisor
{
    private readonly CompactRuntimeEndpointConnectionCoordinator _coordinator;
    private readonly CompactPropertyMap _propertyMap;
    private readonly IRuntimeEndpointReconnectPolicy _reconnectPolicy;
    private readonly CompactEndpointHealthProbeOptions _probeOptions;
    private readonly TimeProvider _timeProvider;

    private readonly object _syncRoot =
        new();

    private Task? _supervisionTask;

    public CompactRuntimeEndpointConnectionSupervisor(
        CompactRuntimeEndpointConnectionCoordinator coordinator,
        CompactPropertyMap propertyMap,
        IRuntimeEndpointReconnectPolicy reconnectPolicy,
        CompactEndpointHealthProbeOptions probeOptions)
        : this(
            coordinator,
            propertyMap,
            reconnectPolicy,
            probeOptions,
            TimeProvider.System)
    {
    }

    internal CompactRuntimeEndpointConnectionSupervisor(
        CompactRuntimeEndpointConnectionCoordinator coordinator,
        CompactPropertyMap propertyMap,
        IRuntimeEndpointReconnectPolicy reconnectPolicy,
        CompactEndpointHealthProbeOptions probeOptions,
        TimeProvider timeProvider)
    {
        _coordinator =
            coordinator
            ?? throw new ArgumentNullException(
                nameof(coordinator));

        _propertyMap =
            propertyMap
            ?? throw new ArgumentNullException(
                nameof(propertyMap));

        _reconnectPolicy =
            reconnectPolicy
            ?? throw new ArgumentNullException(
                nameof(reconnectPolicy));

        _probeOptions =
            probeOptions
            ?? throw new ArgumentNullException(
                nameof(probeOptions));

        _timeProvider =
            timeProvider
            ?? throw new ArgumentNullException(
                nameof(timeProvider));
    }

    public CompactRuntimeEndpointConnectionCoordinator Coordinator =>
        _coordinator;

    public IRuntimeEndpointReconnectPolicy ReconnectPolicy =>
        _reconnectPolicy;

    public CompactEndpointHealthProbeOptions ProbeOptions =>
        _probeOptions;

    public TimeProvider TimeProvider =>
        _timeProvider;

    /// <summary>
    /// Starts supervision or returns the already existing supervision task.
    /// </summary>
    public Task RunAsync(
        CancellationToken cancellationToken = default)
    {
        lock (_syncRoot)
        {
            _supervisionTask ??=
                RunCoreAsync(
                    cancellationToken);

            return _supervisionTask;
        }
    }

    private async Task RunCoreAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await ConnectWithRetryAsync(
                cancellationToken);

            var healthProbe =
                new CompactEndpointHealthProbe(
                    _coordinator,
                    _propertyMap,
                    _probeOptions);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await DelayAsync(
                    _probeOptions.ProbeInterval,
                    cancellationToken);

                try
                {
                    await healthProbe.ProbeAsync(
                        cancellationToken);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    await DetachFaultedConnectionAsync(
                        cancellationToken);

                    await ReconnectWithRetryAsync(
                        cancellationToken);
                }
            }
        }
        finally
        {
            await _coordinator.DisposeAsync();
        }
    }

    private async Task ConnectWithRetryAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await ConnectAttemptAsync(
                reconnect:
                    false,
                cancellationToken);

            return;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
        }

        int retryAttempt =
            0;

        while (true)
        {
            await DelayBeforeReconnectAttemptAsync(
                retryAttempt,
                cancellationToken);

            try
            {
                await ConnectAttemptAsync(
                    reconnect:
                        true,
                    cancellationToken);

                return;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                retryAttempt++;
            }
        }
    }

    private async Task ReconnectWithRetryAsync(
        CancellationToken cancellationToken)
    {
        int retryAttempt =
            0;

        while (true)
        {
            await DelayBeforeReconnectAttemptAsync(
                retryAttempt,
                cancellationToken);

            try
            {
                await ConnectAttemptAsync(
                    reconnect:
                        true,
                    cancellationToken);

                return;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                retryAttempt++;
            }
        }
    }

    /// <summary>
    /// Bounds one compact connection/bootstrap attempt. A COM port can remain
    /// present while the endpoint processor is reset or otherwise silent; such
    /// an attempt must expire so supervision can advance to the reconnect
    /// policy's next attempt.
    /// </summary>
    private async Task ConnectAttemptAsync(
        bool reconnect,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var timeoutTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        timeoutTokenSource.CancelAfter(
            _probeOptions.ProbeTimeout);

        try
        {
            if (reconnect)
            {
                await _coordinator.ReconnectAsync(
                    timeoutTokenSource.Token);
            }
            else
            {
                await _coordinator.ConnectAsync(
                    timeoutTokenSource.Token);
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
            throw new TimeoutException(
                $"Compact endpoint connection attempt timed out after "
                + $"{_probeOptions.ProbeTimeout}.",
                exception);
        }
    }

    private async Task DetachFaultedConnectionAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await _coordinator.DetachFaultedConnectionAsync(
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // The owner removes the connection before awaiting its disposal.
            // A disposal failure must not prevent recovery from opening a new
            // physical connection.
        }
    }

    private async Task DelayBeforeReconnectAttemptAsync(
        int retryAttempt,
        CancellationToken cancellationToken)
    {
        TimeSpan delay =
            _reconnectPolicy.GetDelay(
                retryAttempt);

        await DelayAsync(
            delay,
            cancellationToken);
    }

    private async Task DelayAsync(
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        if (delay <= TimeSpan.Zero)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return;
        }

        await Task.Delay(
            delay,
            _timeProvider,
            cancellationToken);
    }
}