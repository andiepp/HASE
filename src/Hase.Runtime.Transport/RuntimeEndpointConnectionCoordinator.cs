using Hase.Protocol;
using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport;

namespace Hase.Runtime.Transport;

/// <summary>
/// Coordinates the runtime endpoint connection and synchronization
/// lifecycle above the transport connection manager.
/// </summary>
/// <remarks>
/// This implementation establishes or replaces the transport connection,
/// invokes the configured endpoint synchronizer, and transitions the runtime
/// endpoint to ready state after successful synchronization.
///
/// It does not perform automatic retry or retry backoff.
/// </remarks>
public sealed class RuntimeEndpointConnectionCoordinator
    : IRuntimeEndpointProtocolHealthProbe,
      IAsyncDisposable
{
    private readonly TransportConnectionManager _connectionManager;
    private readonly RuntimeEndpoint _runtimeEndpoint;
    private readonly IRuntimeEndpointSynchronizer _synchronizer;

    private readonly TransportExchangeStatisticsCollector
        _protocolExchangeStatisticsCollector =
            new();

    private readonly RuntimeProtocolNotificationSubscriptions
        _notificationSubscriptions =
            new();

    private readonly ProtocolRuntimeEndpointEventRouter
        _eventRouter;

    private RuntimeProtocolConnectionBinding?
        _protocolConnectionBinding;

    private bool _usesProtocolExchangeStatistics;

    private bool _disposed;

    /// <summary>
    /// Initializes a runtime endpoint connection coordinator.
    /// </summary>
    public RuntimeEndpointConnectionCoordinator(
        TransportConnectionManager connectionManager,
        RuntimeEndpoint runtimeEndpoint,
        IRuntimeEndpointSynchronizer synchronizer)
    {
        _connectionManager =
            connectionManager
            ?? throw new ArgumentNullException(
                nameof(connectionManager));

        _runtimeEndpoint =
            runtimeEndpoint
            ?? throw new ArgumentNullException(
                nameof(runtimeEndpoint));

        _synchronizer =
            synchronizer
            ?? throw new ArgumentNullException(
                nameof(synchronizer));

        _eventRouter =
            new ProtocolRuntimeEndpointEventRouter(
                _runtimeEndpoint);

        _notificationSubscriptions.Subscribe(
            _eventRouter);

        _connectionManager.HealthChanged +=
            OnTransportHealthChanged;

        ApplyInitialHealth(
            _connectionManager.GetHealthSnapshot());
    }

    /// <summary>
    /// Gets the transport connection manager owned by the host.
    /// </summary>
    public TransportConnectionManager ConnectionManager =>
        _connectionManager;

    /// <summary>
    /// Gets the runtime endpoint whose lifecycle is coordinated.
    /// </summary>
    public RuntimeEndpoint RuntimeEndpoint =>
        _runtimeEndpoint;

    /// <summary>
    /// Gets the endpoint synchronizer used after transport connection.
    /// </summary>
    public IRuntimeEndpointSynchronizer Synchronizer =>
        _synchronizer;

    /// <summary>
    /// Gets aggregate logical protocol-exchange statistics for duplex
    /// sessions, or transport exchange statistics for legacy connections.
    /// </summary>
    public TransportExchangeStatistics GetExchangeStatistics()
    {
        if (_usesProtocolExchangeStatistics)
        {
            return _protocolExchangeStatisticsCollector
                .GetStatistics();
        }

        return _connectionManager.GetExchangeStatistics();
    }

    /// <summary>
    /// Subscribes an observer to unsolicited protocol notifications.
    /// </summary>
    /// <remarks>
    /// The subscription is retained across duplex session replacement.
    /// Runtime event notifications are also routed automatically into the
    /// coordinator's runtime endpoint.
    /// </remarks>
    public void SubscribeNotification(
        IProtocolNotificationObserver observer)
    {
        ThrowIfDisposed();

        _notificationSubscriptions.Subscribe(
            observer);
    }

    /// <summary>
    /// Removes a protocol-notification observer.
    /// </summary>
    public void UnsubscribeNotification(
        IProtocolNotificationObserver observer)
    {
        ThrowIfDisposed();

        _notificationSubscriptions.Unsubscribe(
            observer);
    }

    /// <inheritdoc />
    public async Task<ProtocolMessage> ProbeAsync(
        ProtocolMessage request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        ArgumentNullException.ThrowIfNull(
            request);

        if (timeout != Timeout.InfiniteTimeSpan
            && timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                timeout,
                "The protocol health-probe timeout must be positive "
                + "or Timeout.InfiniteTimeSpan.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (_runtimeEndpoint.ConnectionStatus.State
            != EndpointConnectionState.Ready)
        {
            throw new InvalidOperationException(
                "A protocol health probe requires a runtime endpoint "
                + "in the Ready state.");
        }

        RuntimeProtocolConnectionBinding binding =
            _protocolConnectionBinding
            ?? throw new InvalidOperationException(
                "The coordinator does not own an active runtime "
                + "protocol connection binding.");

        ITransportConnection transportConnection =
            _connectionManager.CurrentConnection
            ?? throw new InvalidOperationException(
                "The transport connection manager does not own "
                + "a current connection.");

        if (!ReferenceEquals(
                binding.TransportConnection,
                transportConnection))
        {
            throw new InvalidOperationException(
                "The active runtime protocol binding does not match "
                + "the current transport connection.");
        }

        using var timeoutCancellationTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        if (timeout != Timeout.InfiniteTimeSpan)
        {
            timeoutCancellationTokenSource.CancelAfter(
                timeout);
        }

        try
        {
            return await binding.ProtocolConnection.SendAsync(
                request,
                timeoutCancellationTokenSource.Token);
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested
                  && timeoutCancellationTokenSource
                      .IsCancellationRequested)
        {
            InvalidateCurrentTransport(
                transportConnection);

            throw new TimeoutException(
                $"The endpoint did not complete the protocol health "
                + $"probe within {timeout}.",
                exception);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            InvalidateCurrentTransport(
                transportConnection);

            throw;
        }
    }

    /// <summary>
    /// Establishes the initial transport connection and synchronizes the
    /// runtime endpoint.
    /// </summary>
    public async Task<ITransportConnection> ConnectAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        UpdateRuntimeStatus(
            EndpointConnectionState.Connecting,
            DateTimeOffset.UtcNow,
            "Establishing the transport connection.");

        ITransportConnection connection;

        try
        {
            connection =
                await _connectionManager.ConnectAsync(
                    cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            UpdateRuntimeStatus(
                EndpointConnectionState.Disconnected,
                DateTimeOffset.UtcNow,
                "The transport connection attempt was cancelled.");

            throw;
        }
        catch
        {
            UpdateRuntimeStatus(
                EndpointConnectionState.Faulted,
                DateTimeOffset.UtcNow,
                "The transport connection attempt failed.");

            throw;
        }

        await SynchronizeAsync(
            connection,
            cancellationToken);

        return connection;
    }

    /// <summary>
    /// Recovers a faulted runtime endpoint and completely resynchronizes it.
    /// </summary>
    /// <remarks>
    /// A faulted transport connection is replaced before synchronization.
    /// An already-connected transport is retained when only the previous
    /// synchronization attempt failed.
    /// </remarks>
    public async Task<ITransportConnection> ReconnectAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        UpdateRuntimeStatus(
            EndpointConnectionState.Reconnecting,
            DateTimeOffset.UtcNow,
            "Recovering the runtime endpoint connection.");

        ITransportConnection connection;

        try
        {
            connection =
                await GetRecoveryConnectionAsync(
                    cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            UpdateRuntimeStatus(
                EndpointConnectionState.Disconnected,
                DateTimeOffset.UtcNow,
                "The transport reconnection attempt was cancelled.");

            throw;
        }
        catch
        {
            UpdateRuntimeStatus(
                EndpointConnectionState.Faulted,
                DateTimeOffset.UtcNow,
                "The transport reconnection attempt failed.");

            throw;
        }

        await SynchronizeAsync(
            connection,
            cancellationToken);

        return connection;
    }

    /// <summary>
    /// Stops lifecycle coordination and the active protocol receive pump.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed =
            true;

        _connectionManager.HealthChanged -=
            OnTransportHealthChanged;

        RuntimeProtocolConnectionBinding? binding =
            _protocolConnectionBinding;

        _protocolConnectionBinding =
            null;

        if (binding is not null)
        {
            await binding.DisposeAsync();

            DetachProtocolConnectionServices(
                binding);
        }
    }

    private Task<ITransportConnection> GetRecoveryConnectionAsync(
        CancellationToken cancellationToken)
    {
        ITransportConnection connection =
            _connectionManager.CurrentConnection
            ?? throw new InvalidOperationException(
                "The transport connection manager does not own "
                + "a connection to recover.");

        return connection.State switch
        {
            TransportConnectionState.Faulted =>
                _connectionManager.ReplaceFaultedAsync(
                    cancellationToken),

            TransportConnectionState.Connected =>
                Task.FromResult(
                    connection),

            TransportConnectionState.Closed =>
                throw new InvalidOperationException(
                    "A closed transport connection cannot be recovered."),

            _ =>
                throw new InvalidOperationException(
                    $"Unsupported transport connection state "
                    + $"'{connection.State}'.")
        };
    }

    private async Task SynchronizeAsync(
        ITransportConnection connection,
        CancellationToken cancellationToken)
    {
        UpdateRuntimeStatus(
            EndpointConnectionState.Synchronizing,
            _connectionManager.LastStateChangeUtc,
            "Synchronizing the runtime endpoint with the physical endpoint.");

        try
        {
            await SynchronizeThroughAvailableContractAsync(
                connection,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            UpdateRuntimeStatus(
                EndpointConnectionState.Disconnected,
                DateTimeOffset.UtcNow,
                "Endpoint synchronization was cancelled.");

            throw;
        }
        catch
        {
            UpdateRuntimeStatus(
                EndpointConnectionState.Faulted,
                DateTimeOffset.UtcNow,
                "Endpoint synchronization failed.");

            throw;
        }

        UpdateRuntimeStatus(
            EndpointConnectionState.Ready,
            DateTimeOffset.UtcNow,
            "The endpoint is connected, synchronized, and ready.");
    }

    private async Task SynchronizeThroughAvailableContractAsync(
        ITransportConnection connection,
        CancellationToken cancellationToken)
    {
        if (_synchronizer
            is not IRuntimeProtocolEndpointSynchronizer protocolSynchronizer)
        {
            await _synchronizer.SynchronizeAsync(
                connection,
                _runtimeEndpoint,
                cancellationToken);

            return;
        }

        RuntimeProtocolConnectionBinding binding =
            await GetOrCreateProtocolConnectionBindingAsync(
                connection);

        await protocolSynchronizer.SynchronizeAsync(
            binding.ProtocolConnection,
            _runtimeEndpoint,
            cancellationToken);
    }

    private async Task<RuntimeProtocolConnectionBinding>
        GetOrCreateProtocolConnectionBindingAsync(
            ITransportConnection connection)
    {
        RuntimeProtocolConnectionBinding? currentBinding =
            _protocolConnectionBinding;

        if (currentBinding is not null
            && ReferenceEquals(
                currentBinding.TransportConnection,
                connection))
        {
            return currentBinding;
        }

        _protocolConnectionBinding =
            null;

        if (currentBinding is not null)
        {
            await currentBinding.DisposeAsync();

            DetachProtocolConnectionServices(
                currentBinding);
        }

        RuntimeProtocolConnectionBinding replacementBinding =
            RuntimeProtocolConnectionBinding.Create(
                connection);

        AttachProtocolConnectionServices(
            replacementBinding);

        _protocolConnectionBinding =
            replacementBinding;

        return replacementBinding;
    }

    private void AttachProtocolConnectionServices(
        RuntimeProtocolConnectionBinding binding)
    {
        if (binding.ProtocolConnection
            is ITransportExchangeTraceSource traceSource)
        {
            traceSource.SubscribeTrace(
                _protocolExchangeStatisticsCollector);

            _usesProtocolExchangeStatistics =
                true;
        }

        if (binding.ProtocolConnection
            is IRuntimeProtocolNotificationSource notificationSource)
        {
            _notificationSubscriptions.Attach(
                notificationSource);
        }
    }

    private void DetachProtocolConnectionServices(
        RuntimeProtocolConnectionBinding binding)
    {
        if (binding.ProtocolConnection
            is IRuntimeProtocolNotificationSource notificationSource)
        {
            _notificationSubscriptions.Detach(
                notificationSource);
        }

        if (binding.ProtocolConnection
            is ITransportExchangeTraceSource traceSource)
        {
            traceSource.UnsubscribeTrace(
                _protocolExchangeStatisticsCollector);
        }
    }

    private static void InvalidateCurrentTransport(
        ITransportConnection transportConnection)
    {
        if (transportConnection
            is not ITransportConnectionInvalidator invalidator)
        {
            throw new InvalidOperationException(
                "The current transport connection does not support "
                + "health-probe invalidation.");
        }

        invalidator.Invalidate();
    }

    private void ApplyInitialHealth(
        TransportConnectionHealthSnapshot health)
    {
        if (!health.HasConnection)
        {
            UpdateRuntimeStatus(
                EndpointConnectionState.Disconnected,
                health.LastStateChangeUtc,
                "No transport connection is currently available.");

            return;
        }

        ApplyCurrentTransportHealth(
            health);
    }

    private void OnTransportHealthChanged(
        object? sender,
        TransportConnectionHealthChangedEventArgs eventArgs)
    {
        ApplyCurrentTransportHealth(
            eventArgs.CurrentHealth);
    }

    private void ApplyCurrentTransportHealth(
        TransportConnectionHealthSnapshot health)
    {
        if (!health.HasConnection)
        {
            UpdateRuntimeStatus(
                EndpointConnectionState.Disconnected,
                health.LastStateChangeUtc,
                "No transport connection is currently available.");

            return;
        }

        switch (health.State)
        {
            case TransportConnectionState.Connected:
                UpdateRuntimeStatus(
                    EndpointConnectionState.Synchronizing,
                    health.LastStateChangeUtc,
                    "The transport connection is established; "
                    + "endpoint synchronization is required.");
                break;

            case TransportConnectionState.Faulted:
                UpdateRuntimeStatus(
                    EndpointConnectionState.Faulted,
                    health.LastStateChangeUtc,
                    "The transport connection faulted and cannot be reused.");
                break;

            case TransportConnectionState.Closed:
                UpdateRuntimeStatus(
                    EndpointConnectionState.Disconnected,
                    health.LastStateChangeUtc,
                    "The transport connection is closed.");
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported transport connection state "
                    + $"'{health.State}'.");
        }
    }

    private void UpdateRuntimeStatus(
        EndpointConnectionState state,
        DateTimeOffset? changedAtUtc,
        string detail)
    {
        _runtimeEndpoint.UpdateConnectionStatus(
            new EndpointConnectionStatus(
                state,
                changedAtUtc,
                detail));
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }
}