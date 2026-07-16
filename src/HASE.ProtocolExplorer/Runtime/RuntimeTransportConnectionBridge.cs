using Hase.Runtime.Connections;
using Hase.Runtime.Runtime;
using Hase.Transport;

namespace Hase.ProtocolExplorer.Runtime;

/// <summary>
/// Translates transport connection health into the existing runtime
/// endpoint connection-status model.
/// </summary>
internal sealed class RuntimeTransportConnectionBridge
    : IDisposable
{
    private readonly TransportConnectionManager _connectionManager;
    private readonly RuntimeEndpoint _runtimeEndpoint;

    private bool _disposed;

    /// <summary>
    /// Initializes the bridge, subscribes to transport health changes,
    /// and applies the currently observable transport health.
    /// </summary>
    public RuntimeTransportConnectionBridge(
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
            OnHealthChanged;

        ApplyHealth(
            _connectionManager.GetHealthSnapshot());
    }

    /// <summary>
    /// Stops translating transport-health changes.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed =
            true;

        _connectionManager.HealthChanged -=
            OnHealthChanged;
    }

    private void OnHealthChanged(
        object? sender,
        TransportConnectionHealthChangedEventArgs eventArgs)
    {
        ApplyHealth(
            eventArgs.CurrentHealth);
    }

    private void ApplyHealth(
        TransportConnectionHealthSnapshot health)
    {
        ArgumentNullException.ThrowIfNull(
            health);

        EndpointConnectionStatus status =
            CreateConnectionStatus(
                health);

        _runtimeEndpoint.UpdateConnectionStatus(
            status);
    }

    private static EndpointConnectionStatus CreateConnectionStatus(
        TransportConnectionHealthSnapshot health)
    {
        if (!health.HasConnection)
        {
            return new EndpointConnectionStatus(
                EndpointConnectionState.Disconnected,
                health.LastStateChangeUtc,
                "No transport connection is currently available.");
        }

        return health.State switch
        {
            TransportConnectionState.Connected =>
                new EndpointConnectionStatus(
                    EndpointConnectionState.Ready,
                    health.LastStateChangeUtc,
                    "The transport connection is available."),

            TransportConnectionState.Faulted =>
                new EndpointConnectionStatus(
                    EndpointConnectionState.Faulted,
                    health.LastStateChangeUtc,
                    "The transport connection faulted and cannot be reused."),

            TransportConnectionState.Closed =>
                new EndpointConnectionStatus(
                    EndpointConnectionState.Disconnected,
                    health.LastStateChangeUtc,
                    "The transport connection is closed."),

            _ =>
                throw new InvalidOperationException(
                    $"Unsupported transport connection state "
                    + $"'{health.State}'.")
        };
    }
}