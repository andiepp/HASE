namespace Hase.Transport;

/// <summary>
/// Observes completed transport request/response exchanges.
/// </summary>
public interface ITransportExchangeTraceObserver
{
    /// <summary>
    /// Receives one completed transport exchange trace.
    /// </summary>
    void OnTransportExchangeCompleted(
        TransportExchangeTrace trace);
}