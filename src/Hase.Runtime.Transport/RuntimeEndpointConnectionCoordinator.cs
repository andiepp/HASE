using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport;

namespace Hase.Runtime.Transport;

/// <summary>
/// Coordinates the runtime endpoint connection and synchronization
/// lifecycle above the transport connection manager.
/// </summary>
/// <remarks>
/// This implementation establishes the transport connection, invokes the
/// configured endpoint synchronizer, and transitions the runtime endpoint
/// to ready state after successful synchronization.
///
/// It does not perform automatic reconnect, retry, or retry backoff.
/// </remarks>
public sealed class RuntimeEndpointConnectionCoordinator
    : IAsyncDisposable
{
    private readonly TransportConnectionManager _connectionManager;
    private readonly RuntimeEndpoint _runtimeEndpoint;
    private readonly IRuntimeEndpointSynchronizer _synchronizer;

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
    /// Establishes the initial transport connection and synchronizes the
    /// runtime endpoint.
    /// </summary>
    /// <remarks>
    /// Successful completion transitions the runtime endpoint to
    /// <see cref="EndpointConnectionState.Ready"/>.
    /// </remarks>
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

        UpdateRuntimeStatus(
            EndpointConnectionState.Synchronizing,
            _connectionManager.LastStateChangeUtc,
            "Synchronizing the runtime endpoint with the physical endpoint.");

        try
        {
            await _synchronizer.SynchronizeAsync(
                connection,
                _runtimeEndpoint,
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

        return connection;
    }

    /// <summary>
    /// Stops lifecycle coordination.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed =
            true;

        _connectionManager.HealthChanged -=
            OnTransportHealthChanged;

        return ValueTask.CompletedTask;
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