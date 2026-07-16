using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport;

namespace Hase.Runtime.Transport;

/// <summary>
/// Coordinates the runtime endpoint connection lifecycle above the
/// transport connection manager.
/// </summary>
/// <remarks>
/// This initial implementation establishes the transport connection and
/// transitions the runtime endpoint into synchronization state.
///
/// It does not perform protocol discovery, descriptor synchronization,
/// automatic reconnect, retry, or runtime-cache restoration.
/// </remarks>
public sealed class RuntimeEndpointConnectionCoordinator
    : IAsyncDisposable
{
    private readonly TransportConnectionManager _connectionManager;
    private readonly RuntimeEndpoint _runtimeEndpoint;

    private bool _disposed;

    /// <summary>
    /// Initializes a runtime endpoint connection coordinator.
    /// </summary>
    public RuntimeEndpointConnectionCoordinator(
        TransportConnectionManager connectionManager,
        RuntimeEndpoint runtimeEndpoint)
    {
        _connectionManager =
            connectionManager
            ?? throw new ArgumentNullException(
                nameof(connectionManager));

        _runtimeEndpoint =
            runtimeEndpoint
            ?? throw new ArgumentNullException(
                nameof(runtimeEndpoint));

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
    /// Establishes the initial transport connection.
    /// </summary>
    /// <remarks>
    /// A successful transport connection transitions the runtime endpoint
    /// to <see cref="EndpointConnectionState.Synchronizing"/>.
    /// A later protocol synchronization operation must transition it to
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

        try
        {
            return await _connectionManager.ConnectAsync(
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
