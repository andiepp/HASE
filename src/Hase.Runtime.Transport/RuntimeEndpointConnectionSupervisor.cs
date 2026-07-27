using Hase.Runtime.Connections;

namespace Hase.Runtime.Transport;

/// <summary>
/// Supervises the connection lifecycle of one runtime endpoint.
/// </summary>
/// <remarks>
/// One supervisor instance owns one supervision loop. Repeated calls to
/// <see cref="RunAsync"/> return the same supervision task.
///
/// The supervisor retries both initial connection failures and recovery after
/// an established transport connection faults.
/// </remarks>
public sealed class RuntimeEndpointConnectionSupervisor
    : IEndpointConnectionStatusObserver
{
    private readonly RuntimeEndpointConnectionCoordinator _coordinator;
    private readonly IRuntimeEndpointReconnectPolicy _reconnectPolicy;
    private readonly NativeEndpointHealthProbeOptions _probeOptions;
    private readonly TimeProvider _timeProvider;

    private readonly SemaphoreSlim _statusChanged =
        new(
            initialCount: 0);

    private readonly object _syncRoot =
        new();

    private Task? _supervisionTask;

    private long _initialConnectionAttemptCount;
    private long _initialConnectionFailureCount;
    private long _reconnectAttemptCount;
    private long _reconnectFailureCount;
    private long _successfulRecoveryCount;

    private DateTimeOffset? _lastRecoveryStartedAtUtc;
    private DateTimeOffset? _lastRecoveryCompletedAtUtc;
    private TimeSpan? _lastRecoveryDuration;

    /// <summary>
    /// Initializes a runtime endpoint connection supervisor using the system
    /// time provider.
    /// </summary>
    public RuntimeEndpointConnectionSupervisor(
        RuntimeEndpointConnectionCoordinator coordinator,
        IRuntimeEndpointReconnectPolicy reconnectPolicy)
        : this(
            coordinator,
            reconnectPolicy,
            NativeEndpointHealthProbeOptions.Default,
            TimeProvider.System)
    {
    }

    /// <summary>
    /// Initializes a runtime endpoint connection supervisor using the system
    /// time provider and explicit native health-probe options.
    /// </summary>
    public RuntimeEndpointConnectionSupervisor(
        RuntimeEndpointConnectionCoordinator coordinator,
        IRuntimeEndpointReconnectPolicy reconnectPolicy,
        NativeEndpointHealthProbeOptions probeOptions)
        : this(
            coordinator,
            reconnectPolicy,
            probeOptions,
            TimeProvider.System)
    {
    }

    /// <summary>
    /// Initializes a runtime endpoint connection supervisor.
    /// </summary>
    public RuntimeEndpointConnectionSupervisor(
        RuntimeEndpointConnectionCoordinator coordinator,
        IRuntimeEndpointReconnectPolicy reconnectPolicy,
        TimeProvider timeProvider)
        : this(
            coordinator,
            reconnectPolicy,
            NativeEndpointHealthProbeOptions.Default,
            timeProvider)
    {
    }

    internal RuntimeEndpointConnectionSupervisor(
        RuntimeEndpointConnectionCoordinator coordinator,
        IRuntimeEndpointReconnectPolicy reconnectPolicy,
        NativeEndpointHealthProbeOptions probeOptions,
        TimeProvider timeProvider)
    {
        _coordinator =
            coordinator
            ?? throw new ArgumentNullException(
                nameof(coordinator));

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

    /// <summary>
    /// Gets the coordinator used by this supervisor.
    /// </summary>
    public RuntimeEndpointConnectionCoordinator Coordinator =>
        _coordinator;

    /// <summary>
    /// Gets the reconnect policy used by connection supervision.
    /// </summary>
    public IRuntimeEndpointReconnectPolicy ReconnectPolicy =>
        _reconnectPolicy;

    /// <summary>
    /// Gets the native endpoint health-probe timing.
    /// </summary>
    public NativeEndpointHealthProbeOptions ProbeOptions =>
        _probeOptions;

    /// <summary>
    /// Gets the time provider used for recovery timing.
    /// </summary>
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

    /// <summary>
    /// Gets an immutable snapshot of the current supervision statistics.
    /// </summary>
    public RuntimeEndpointConnectionStatistics GetStatistics()
    {
        lock (_syncRoot)
        {
            return new RuntimeEndpointConnectionStatistics(
                _initialConnectionAttemptCount,
                _initialConnectionFailureCount,
                _reconnectAttemptCount,
                _reconnectFailureCount,
                _successfulRecoveryCount,
                _lastRecoveryStartedAtUtc,
                _lastRecoveryCompletedAtUtc,
                _lastRecoveryDuration);
        }
    }

    /// <inheritdoc />
    public void OnEndpointConnectionStatusChanged(
        EndpointConnectionStatusChanged change)
    {
        ArgumentNullException.ThrowIfNull(
            change);

        if (!ReferenceEquals(
            change.Endpoint,
            _coordinator.RuntimeEndpoint))
        {
            return;
        }

        _statusChanged.Release();
    }

    private async Task RunCoreAsync(
        CancellationToken cancellationToken)
    {
        _coordinator.RuntimeEndpoint.SubscribeConnectionStatus(
            this);

        try
        {
            await ConnectWithRetryAsync(
                cancellationToken);

            var healthProbe =
                new NativeEndpointHealthProbe(
                    _coordinator,
                    _probeOptions);

            while (true)
            {
                await MonitorReadyConnectionAsync(
                    healthProbe,
                    cancellationToken);

                await RecoverWithRetryAsync(
                    cancellationToken);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            _coordinator.RuntimeEndpoint.UpdateConnectionStatus(
                new EndpointConnectionStatus(
                    EndpointConnectionState.Disconnected,
                    _timeProvider.GetUtcNow(),
                    "Endpoint connection supervision was cancelled."));

            throw;
        }
        finally
        {
            _coordinator.RuntimeEndpoint.UnsubscribeConnectionStatus(
                this);
        }
    }

    private async Task ConnectWithRetryAsync(
        CancellationToken cancellationToken)
    {
        RecordInitialConnectionAttempt();

        try
        {
            await _coordinator.ConnectAsync(
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
            RecordInitialConnectionFailure();
        }

        int retryAttempt =
            0;

        while (true)
        {
            await DelayBeforeAttemptAsync(
                retryAttempt,
                cancellationToken);

            RecordInitialConnectionAttempt();

            try
            {
                await _coordinator.ConnectAsync(
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
                RecordInitialConnectionFailure();

                retryAttempt++;
            }
        }
    }

    private async Task MonitorReadyConnectionAsync(
        NativeEndpointHealthProbe healthProbe,
        CancellationToken cancellationToken)
    {
        while (_coordinator.RuntimeEndpoint.ConnectionStatus.State
            != EndpointConnectionState.Faulted)
        {
            bool statusChanged =
                await _statusChanged.WaitAsync(
                _probeOptions.ProbeInterval,
                cancellationToken);

            if (_coordinator.RuntimeEndpoint.ConnectionStatus.State
                == EndpointConnectionState.Faulted)
            {
                return;
            }

            if (statusChanged)
            {
                continue;
            }

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
                if (_coordinator.RuntimeEndpoint.ConnectionStatus.State
                    != EndpointConnectionState.Faulted)
                {
                    throw;
                }
            }
        }
    }

    private async Task RecoverWithRetryAsync(
        CancellationToken cancellationToken)
    {
        DateTimeOffset recoveryStartedAtUtc =
            _timeProvider.GetUtcNow();

        long recoveryStartTimestamp =
            _timeProvider.GetTimestamp();

        RecordRecoveryStarted(
            recoveryStartedAtUtc);

        int retryAttempt =
            0;

        while (true)
        {
            await DelayBeforeAttemptAsync(
                retryAttempt,
                cancellationToken);

            RecordReconnectAttempt();

            try
            {
                await _coordinator.ReconnectAsync(
                    cancellationToken);

                DateTimeOffset recoveryCompletedAtUtc =
                    _timeProvider.GetUtcNow();

                TimeSpan recoveryDuration =
                    _timeProvider.GetElapsedTime(
                        recoveryStartTimestamp,
                        _timeProvider.GetTimestamp());

                RecordSuccessfulRecovery(
                    recoveryCompletedAtUtc,
                    recoveryDuration);

                return;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                RecordReconnectFailure();

                retryAttempt++;
            }
        }
    }

    private async Task DelayBeforeAttemptAsync(
        int retryAttempt,
        CancellationToken cancellationToken)
    {
        TimeSpan delay =
            _reconnectPolicy.GetDelay(
                retryAttempt);

        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(
                delay,
                cancellationToken);
        }
    }

    private void RecordInitialConnectionAttempt()
    {
        lock (_syncRoot)
        {
            _initialConnectionAttemptCount++;
        }
    }

    private void RecordInitialConnectionFailure()
    {
        lock (_syncRoot)
        {
            _initialConnectionFailureCount++;
        }
    }

    private void RecordRecoveryStarted(
        DateTimeOffset startedAtUtc)
    {
        lock (_syncRoot)
        {
            _lastRecoveryStartedAtUtc =
                startedAtUtc;
        }
    }

    private void RecordReconnectAttempt()
    {
        lock (_syncRoot)
        {
            _reconnectAttemptCount++;
        }
    }

    private void RecordReconnectFailure()
    {
        lock (_syncRoot)
        {
            _reconnectFailureCount++;
        }
    }

    private void RecordSuccessfulRecovery(
        DateTimeOffset completedAtUtc,
        TimeSpan duration)
    {
        lock (_syncRoot)
        {
            _successfulRecoveryCount++;

            _lastRecoveryCompletedAtUtc =
                completedAtUtc;

            _lastRecoveryDuration =
                duration;
        }
    }
}
