namespace Hase.Runtime.Transport;

/// <summary>
/// Provides combined diagnostic snapshots for runtime endpoint connection
/// supervisors.
/// </summary>
public static class RuntimeEndpointConnectionSupervisorDiagnosticsExtensions
{
    /// <summary>
    /// Creates an immutable combined diagnostic snapshot for the supervised
    /// runtime endpoint connection.
    /// </summary>
    public static RuntimeEndpointConnectionDiagnostics GetDiagnostics(
        this RuntimeEndpointConnectionSupervisor supervisor)
    {
        ArgumentNullException.ThrowIfNull(
            supervisor);

        Hase.Transport.TransportConnectionManager connectionManager =
            supervisor.Coordinator.ConnectionManager;

        return new RuntimeEndpointConnectionDiagnostics(
            transportHealth:
                connectionManager.GetHealthSnapshot(),
            connectionStatistics:
                supervisor.GetStatistics(),
            exchangeStatistics:
                connectionManager.GetExchangeStatistics());
    }
}