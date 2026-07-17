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

    private readonly SemaphoreSlim _statusChanged =
        new(
            initialCount: 0);

    private readonly object _syncRoot =
        new();

    private Task? _supervisionTask;

    /// <summary>
    /// Initializes a runtime endpoint connection supervisor.
    /// </summary>
    public RuntimeEndpointConnectionSupervisor(
        RuntimeEndpointConnectionCoordinator coordinator,
        IRuntimeEndpointReconnectPolicy reconnectPolicy)
    {
        _coordinator =
            coordinator
            ?? throw new ArgumentNullException(
                nameof(coordinator));

        _reconnectPolicy =
            reconnectPolicy
            ?? throw new ArgumentNullException(
                nameof(reconnectPolicy));
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
    /// Starts supervision or returns the already existing supervision task.
    /// </summary>
    /// <remarks>
    /// The cancellation token supplied to the first call owns the supervision
    /// loop. Tokens supplied by later calls are ignored because those calls
    /// join the existing loop.
    /// </remarks>
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

            while (true)
            {
                await WaitForFaultAsync(
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
                    DateTimeOffset.UtcNow,
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
        }

        int retryAttempt =
            0;

        while (true)
        {
            await DelayBeforeAttemptAsync(
                retryAttempt,
                cancellationToken);

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
                retryAttempt++;
            }
        }
    }

    private async Task WaitForFaultAsync(
        CancellationToken cancellationToken)
    {
        while (_coordinator.RuntimeEndpoint.ConnectionStatus.State
            != EndpointConnectionState.Faulted)
        {
            await _statusChanged.WaitAsync(
                cancellationToken);
        }
    }

    private async Task RecoverWithRetryAsync(
        CancellationToken cancellationToken)
    {
        int retryAttempt =
            0;

        while (true)
        {
            await DelayBeforeAttemptAsync(
                retryAttempt,
                cancellationToken);

            try
            {
                await _coordinator.ReconnectAsync(
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
}