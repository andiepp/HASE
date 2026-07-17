using Hase.Transport;

namespace Hase.Runtime.Transport;

/// <summary>
/// Provides an immutable combined diagnostic snapshot for one supervised
/// runtime endpoint connection.
/// </summary>
/// <remarks>
/// The snapshot combines transport health, endpoint connection-supervision
/// statistics, and aggregate transport-exchange statistics.
/// </remarks>
public sealed record RuntimeEndpointConnectionDiagnostics
{
    /// <summary>
    /// Initializes a combined runtime endpoint connection diagnostic snapshot.
    /// </summary>
    public RuntimeEndpointConnectionDiagnostics(
        TransportConnectionHealthSnapshot transportHealth,
        RuntimeEndpointConnectionStatistics connectionStatistics,
        TransportExchangeStatistics exchangeStatistics)
    {
        TransportHealth =
            transportHealth
            ?? throw new ArgumentNullException(
                nameof(transportHealth));

        ConnectionStatistics =
            connectionStatistics
            ?? throw new ArgumentNullException(
                nameof(connectionStatistics));

        ExchangeStatistics =
            exchangeStatistics
            ?? throw new ArgumentNullException(
                nameof(exchangeStatistics));
    }

    /// <summary>
    /// Gets an empty diagnostic snapshot.
    /// </summary>
    public static RuntimeEndpointConnectionDiagnostics Empty
    {
        get;
    } =
        new(
            transportHealth:
                new TransportConnectionHealthSnapshot(
                    hasConnection:
                        false,
                    state:
                        null,
                    lastStateChangeUtc:
                        null,
                    replacementCount:
                        0),
            connectionStatistics:
                RuntimeEndpointConnectionStatistics.Empty,
            exchangeStatistics:
                TransportExchangeStatistics.Empty);

    /// <summary>
    /// Gets the current transport connection-health snapshot.
    /// </summary>
    public TransportConnectionHealthSnapshot TransportHealth
    {
        get;
    }

    /// <summary>
    /// Gets the endpoint connection-supervision statistics.
    /// </summary>
    public RuntimeEndpointConnectionStatistics ConnectionStatistics
    {
        get;
    }

    /// <summary>
    /// Gets the aggregate transport-exchange statistics.
    /// </summary>
    public TransportExchangeStatistics ExchangeStatistics
    {
        get;
    }
}
